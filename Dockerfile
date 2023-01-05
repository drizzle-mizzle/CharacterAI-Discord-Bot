FROM mcr.microsoft.com/dotnet/sdk:7.0-bullseye-slim as build-env

WORKDIR /app
COPY *.sln .
COPY *.csproj .

RUN dotnet restore ./CharacterAI_Discord_Bot.sln
COPY . .
RUN dotnet publish ./CharacterAI_Discord_Bot.csproj --configuration Release --output out --self-contained

FROM mcr.microsoft.com/dotnet/runtime:7.0-bullseye-slim AS run-env

COPY --from=build-env /app/out/ .

RUN echo '#!/bin/bash \n ./CharacterAI_Discord_Bot' > ./entrypoint.sh
RUN chmod +x ./entrypoint.sh
ENTRYPOINT ["./entrypoint.sh"]