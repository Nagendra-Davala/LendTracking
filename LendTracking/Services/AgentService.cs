using System.Runtime.CompilerServices;
using LendTracking.Model.Agent;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace LendTracking.Services
{
    public record AgentStreamEvent(string Event, object Data)
    {
        public static AgentStreamEvent Token(string text) => new("token", new { text });
        public static AgentStreamEvent ToolCall(string tool) => new("tool_call", new { tool, status = "running" });
        public static AgentStreamEvent ProposedAction(string action, string payloadJson, string confirmToken) =>
            new("proposed_action", new { action, payload = System.Text.Json.JsonSerializer.Deserialize<object>(payloadJson), confirmToken });
        public static AgentStreamEvent Error(string message) => new("error", new { message });
        public static AgentStreamEvent Done() => new("done", new { });
    }

    public class AgentService
    {
        private const string SystemPrompt =
            "You are the LendTracker assistant, reachable by voice and text inside a small chat panel. " +
            "You can do exactly four things, nothing else: (1) answer questions about the user's real " +
            "lending records and interest using the read tools, (2) draft adding a new record via " +
            "ProposeAddLending, (3) draft marking a record cleared/reopened via ProposeSetCleared, " +
            "(4) draft permanently deleting a record via ProposeDeleteLending. All Propose* tools only " +
            "draft the action for the user to confirm in the UI — they never save/delete anything themselves; " +
            "state in one short sentence what you're proposing and that it needs confirming, never claim it's " +
            "already done. If the user asks for anything else (groups, expenses, settlements, anything not " +
            "in that list of four), say plainly that you can't do that yet — do not attempt it, and do not " +
            "produce vague or evasive text instead of a real answer. " +
            "STRICT OUTPUT RULE: every reply must be either one direct question or one direct answer — " +
            "1-2 short sentences, no exceptions. Never output meta-commentary about the conversation itself " +
            "(never say things like 'we'll wait for your answer', 'no output', or describe what you're about " +
            "to do next) — just say the actual question or answer, nothing wrapped around it. Never add " +
            "caveats, restated context, or closing offers to help. Never invent numbers that didn't come from " +
            "a tool, and never use markdown tables (the pipe '|' syntax) — for a list of records use one short " +
            "bullet line per record (e.g. '- TankalaRavi: ₹5,000 at 2%, given 2026-09-28, active') and point " +
            "to the full lending history page for the detailed table. " +
            "LANGUAGE: many users interact entirely by voice and may be low-literacy. Always reply in the " +
            "exact same language and script the user just wrote or spoke in — never switch to English unless " +
            "they used English, even mid-conversation. Before sending a reply, check it is actually written " +
            "in that language; if you drafted it in English by habit, translate it before responding. " +
            "AMBIGUOUS INTENT: voice transcripts, especially in regional languages, are often garbled or " +
            "missing words — do not silently guess when a transcript could plausibly mean two very different " +
            "things (e.g. add vs. clear vs. delete). Ask one short plain-language question naming the likely " +
            "options first, and wait for a clear answer before calling any tool that changes data. " +
            "COLLECTING FIELDS: when adding a record, ask for ONE missing field at a time as a single short " +
            "question — never list multiple fields in one message — waiting for each answer before the next " +
            "(order: counterparty name, direction Lent/Borrowed, principal amount, annual interest rate, date, " +
            "then optional notes last). " +
            "RESOLVING WHICH RECORD: NEVER ask the user to say or type a numeric record ID — it means nothing " +
            "to them and voice-transcribed numbers are unreliable. Call ListLendingRecords yourself, then " +
            "match what the user already told you (counterparty name, roughly when, amount, or 'the active " +
            "one' / 'the cleared one') to resolve the ID internally. If more than one record could match, " +
            "list the candidates back using only those natural details (name, amount, date, status) — never " +
            "mention the word 'ID' — and ask the user to pick by describing which one. Only call " +
            "ProposeSetCleared or ProposeDeleteLending once you're confident which single record matches.";

        private static readonly HashSet<string> ProposalFunctions = new(StringComparer.OrdinalIgnoreCase)
        {
            "ProposeAddLending",
            "ProposeSetCleared",
            "ProposeDeleteLending"
        };

        private readonly LendingService _lendingService;
        private readonly SimpleInterestService _simpleInterestService;
        private readonly PendingActionStore _pendingActions;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly Uri _endpoint;

        public AgentService(
            LendingService lendingService,
            SimpleInterestService simpleInterestService,
            PendingActionStore pendingActions,
            IConfiguration config)
        {
            _lendingService = lendingService;
            _simpleInterestService = simpleInterestService;
            _pendingActions = pendingActions;

            _apiKey = config["Groq:ApiKey"] ?? throw new InvalidOperationException("Groq:ApiKey is not configured.");
            _model = config["Groq:Model"] ?? "llama-3.3-70b-versatile";
            _endpoint = new Uri(config["Groq:Endpoint"] ?? "https://api.groq.com/openai/v1");
        }

        public async IAsyncEnumerable<AgentStreamEvent> StreamChatAsync(
            int userId,
            List<ChatMessageDto> messages,
            string? languageHint,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            Kernel kernel;
            ChatHistory history;
            IChatCompletionService chat;
            PromptExecutionSettings settings;
            IAsyncEnumerator<StreamingChatMessageContent> enumerator;
            AgentStreamEvent? setupError = null;

            try
            {
                var builder = Kernel.CreateBuilder();
#pragma warning disable SKEXP0010
                builder.AddOpenAIChatCompletion(modelId: _model, apiKey: _apiKey, endpoint: _endpoint);
#pragma warning restore SKEXP0010
                kernel = builder.Build();

                var tools = new AgentTools(userId, _lendingService, _simpleInterestService);
                kernel.Plugins.AddFromObject(tools, "Lending");

                var effectivePrompt = string.IsNullOrWhiteSpace(languageHint)
                    ? SystemPrompt
                    : SystemPrompt + $" The user's language for this entire conversation is {languageHint} — " +
                      $"reply only in {languageHint}, in its native script, no matter what script or language " +
                      "any individual message happens to be written in.";

                history = new ChatHistory(effectivePrompt);
                foreach (var m in messages)
                {
                    history.AddMessage(
                        string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase) ? AuthorRole.Assistant : AuthorRole.User,
                        m.Content);
                }

                chat = kernel.GetRequiredService<IChatCompletionService>();
                settings = new PromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };
                enumerator = chat.GetStreamingChatMessageContentsAsync(history, settings, kernel, ct).GetAsyncEnumerator(ct);
            }
            catch (Exception ex)
            {
                setupError = AgentStreamEvent.Error($"Setup failed: {ex.Message}");
                kernel = null!;
                history = null!;
                chat = null!;
                settings = null!;
                enumerator = null!;
            }

            if (setupError is not null)
            {
                yield return setupError;
                yield break;
            }

            var seenFunctionResults = 0;
            var streamedAny = false;

            try
            {
                while (true)
                {
                    bool hasNext;
                    StreamingChatMessageContent? chunk = null;
                    AgentStreamEvent? failure = null;
                    try
                    {
                        hasNext = await enumerator.MoveNextAsync();
                        if (hasNext) chunk = enumerator.Current;
                    }
                    catch (Exception ex)
                    {
                        hasNext = false;
                        failure = AgentStreamEvent.Error(ex.Message);
                    }

                    if (failure is not null)
                    {
                        yield return failure;
                        yield break;
                    }

                    if (!hasNext) break;

                    if (!string.IsNullOrEmpty(chunk!.Content))
                    {
                        streamedAny = true;
                        yield return AgentStreamEvent.Token(chunk.Content);
                    }

                    // Any FunctionResultContent the connector appended to `history` mid-loop
                    // represents a tool call that already finished — surface proposals as they land.
                    foreach (var proposed in ExtractNewProposals(history, ref seenFunctionResults, userId))
                    {
                        yield return proposed;
                    }
                }
            }
            finally
            {
                await enumerator.DisposeAsync();
            }

            foreach (var proposed in ExtractNewProposals(history, ref seenFunctionResults, userId))
            {
                yield return proposed;
            }

            if (!streamedAny)
            {
                yield return AgentStreamEvent.Token("Done.");
            }

            yield return AgentStreamEvent.Done();
        }

        private IEnumerable<AgentStreamEvent> ExtractNewProposals(ChatHistory history, ref int seen, int userId)
        {
            var results = history
                .SelectMany(m => m.Items)
                .OfType<Microsoft.SemanticKernel.FunctionResultContent>()
                .Where(f => f.FunctionName is not null && ProposalFunctions.Contains(f.FunctionName))
                .ToList();

            var events = new List<AgentStreamEvent>();
            for (var i = seen; i < results.Count; i++)
            {
                var result = results[i];
                var functionName = result.FunctionName!;
                var json = result.Result?.ToString();
                if (string.IsNullOrWhiteSpace(json)) continue;

                var token = _pendingActions.Add(userId, functionName, json);
                events.Add(AgentStreamEvent.ProposedAction(functionName, json, token));
            }
            seen = results.Count;
            return events;
        }
    }
}
