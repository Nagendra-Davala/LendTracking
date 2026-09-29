using System.Net.Http.Headers;
using System.Text.Json;

namespace LendTracking.Services
{
    public record TranscriptionResult(string Text, string? Language);

    public class SpeechService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly string _endpoint;

        public SpeechService(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _http = httpClientFactory.CreateClient();
            _apiKey = config["Groq:ApiKey"] ?? throw new InvalidOperationException("Groq:ApiKey is not configured.");
            _model = config["Groq:WhisperModel"] ?? "whisper-large-v3-turbo";
            _endpoint = config["Groq:Endpoint"] ?? "https://api.groq.com/openai/v1";
        }

        public async Task<TranscriptionResult> TranscribeAsync(Stream audio, string fileName, string? contentType, string? language, CancellationToken ct)
        {
            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(audio);
            streamContent.Headers.ContentType = MediaTypeHeaderValue.TryParse(contentType, out var mt)
                ? mt
                : new MediaTypeHeaderValue("audio/webm");
            content.Add(streamContent, "file", string.IsNullOrWhiteSpace(fileName) ? "audio.webm" : fileName);
            content.Add(new StringContent(_model), "model");
            content.Add(new StringContent("verbose_json"), "response_format");

            // An explicit hint skips Whisper's own language-ID step, which is far less reliable
            // than English/Hindi for lower-resource languages like Telugu on short clips.
            if (!string.IsNullOrWhiteSpace(language))
            {
                content.Add(new StringContent(language), "language");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_endpoint}/audio/transcriptions") { Content = content };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            using var response = await _http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Transcription failed ({(int)response.StatusCode}): {body}");
            }

            using var doc = JsonDocument.Parse(body);
            var text = doc.RootElement.TryGetProperty("text", out var t) ? t.GetString() ?? string.Empty : string.Empty;
            var detectedLanguage = doc.RootElement.TryGetProperty("language", out var l) ? l.GetString() : null;

            return new TranscriptionResult(text.Trim(), detectedLanguage);
        }
    }
}
