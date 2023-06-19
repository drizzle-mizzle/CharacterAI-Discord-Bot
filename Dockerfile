FROM mcr.microsoft.com/dotnet/sdk:7.0-bullseye-slim as build-env

WORKDIR /app
COPY *.sln .
COPY *.csproj .

RUN dotnet restore ./CharacterAI_Discord_Bot.sln
COPY . .
RUN dotnet publish ./CharacterAI_Discord_Bot.csproj --configuration Release --output out --self-contained

FROM mcr.microsoft.com/dotnet/runtime:7.0-bullseye-slim AS run-env

COPY --from=build-env /app/out/ .

# install dependencies
RUN apt-get update -y && apt-get install -y libgtk-3-dev libnotify-dev libgconf-2-4 libnss3 libxss1 libasound2

RUN echo '#!/bin/bash \n ./CharacterAI_Discord_Bot' > ./entrypoint.sh
RUN chmod +x ./entrypoint.sh
ENTRYPOINT ["./entrypoint.sh"]
