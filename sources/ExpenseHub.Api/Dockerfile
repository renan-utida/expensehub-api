# Image of the ExpenseHub web application.
#
# The build context is the root of the repository:
#   docker build -f sources/ExpenseHub.Api/Dockerfile --build-arg GIT_COMMIT=<full commit sha> -t <name>:<tag> .
#
# The web image and the worker image must use the same base images: keep the ARG and FROM lines of
# sources/ExpenseHub.Worker/Dockerfile equal to these.
ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.403
ARG DOTNET_RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0.14

FROM ${DOTNET_SDK_IMAGE} AS build
WORKDIR /src
COPY Directory.Build.props .editorconfig ./
COPY sources/ExpenseHub.Api/ExpenseHub.Api.csproj sources/ExpenseHub.Api/
RUN dotnet restore sources/ExpenseHub.Api/ExpenseHub.Api.csproj
COPY sources/ExpenseHub.Api/ sources/ExpenseHub.Api/
RUN dotnet publish sources/ExpenseHub.Api/ExpenseHub.Api.csproj --configuration Release --no-restore --output /app/publish -p:UseAppHost=false

FROM ${DOTNET_RUNTIME_IMAGE} AS final

# /data holds the SQLite file and /keys holds the keys of the data protection. Both are mount points of named volumes: a new
# volume copies the owner of the directory of the image, so they are handed to the user of the image (not root) here.
USER root
RUN mkdir -p /data /keys && chown "$APP_UID" /data /keys
USER $APP_UID

WORKDIR /app
COPY --from=build /app/publish .

# The commit is the evidence of which version runs: it is an environment variable (read as Build:Commit and written in the log
# when the process starts) and an OCI label. These lines come last so a new commit does not invalidate the restore cache.
ARG GIT_COMMIT=unknown
ARG SOURCE_URL=https://github.com/renan-utida/expensehub-api
ENV ASPNETCORE_HTTP_PORTS=8080 \
    Build__Commit=${GIT_COMMIT}
LABEL org.opencontainers.image.title="expensehub-web" \
      org.opencontainers.image.description="ExpenseHub web application (REST API of the expense reimbursements and the reports)" \
      org.opencontainers.image.source="${SOURCE_URL}" \
      org.opencontainers.image.revision="${GIT_COMMIT}"

EXPOSE 8080
ENTRYPOINT ["dotnet", "ExpenseHub.Api.dll"]
