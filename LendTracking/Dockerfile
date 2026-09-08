# Use the official Microsoft .NET SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy your csproj and restore dependencies
COPY ["LendTracking.csproj", "./"]
RUN dotnet restore

# Copy the rest of your code and build it
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Use the runtime image to run the app
FROM ://microsoft.com AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render dynamically assigns a port, so .NET must listen to it
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "LendTracking.dll"]
