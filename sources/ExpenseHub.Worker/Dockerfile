# Image of the ExpenseHub worker, which consumes the queue of the reports.
#
# The build context is the root of the repository:
#   docker build -f sources/ExpenseHub.Worker/Dockerfile --build-arg GIT_COMMIT=<full commit sha> -t <name>:<tag> .
#
# The worker project references the web project and so inherits the framework Microsoft.AspNetCore.App: the runtime image is
# aspnet, not the smaller runtime image. The web image and the worker image must use the same base images: keep the ARG and
# FROM lines of sources/ExpenseHub.Api/Dockerfile equal to these.
ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.403
ARG DOTNET_RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0.14

FROM ${DOTNET_SDK_IMAGE} AS build
WORKDIR /src
COPY Directory.Build.props .editorconfig ./
COPY sources/ExpenseHub.Api/ExpenseHub.Api.csproj sources/ExpenseHub.Api/
COPY sources/ExpenseHub.Worker/ExpenseHub.Worker.csproj sources/ExpenseHub.Worker/
RUN dotnet restore sources/ExpenseHub.Worker/ExpenseHub.Worker.csproj
COPY sources/ExpenseHub.Api/ sources/ExpenseHub.Api/
COPY sources/ExpenseHub.Worker/ sources/ExpenseHub.Worker/
RUN dotnet publish sources/ExpenseHub.Worker/ExpenseHub.Worker.csproj --configuration Release --no-restore --output /app/publish -p:UseAppHost=false

FROM ${DOTNET_RUNTIME_IMAGE} AS final

# /data holds the SQLite file that the web application shares with the worker. It is the mount point of a named volume: a new
# volume copies the owner of the directory of the image, so it is handed to the user of the image (not root) here. The user is
# the same as in the web image, which is what lets both processes write to the same file.
USER root
RUN mkdir -p /data && chown "$APP_UID" /data
USER $APP_UID

WORKDIR /app
COPY --from=build /app/publish .

# The commit is the evidence of which version runs: it is an environment variable (read as Build:Commit and written in the log
# when the process starts) and an OCI label. These lines come last so a new commit does not invalidate the restore cache.
ARG GIT_COMMIT=unknown
ARG SOURCE_URL=https://github.com/renan-utida/expensehub-api
ENV Build__Commit=${GIT_COMMIT}
LABEL org.opencontainers.image.title="expensehub-worker" \
      org.opencontainers.image.description="ExpenseHub worker (consumes the report queue, calculates and saves the result)" \
      org.opencontainers.image.source="${SOURCE_URL}" \
      org.opencontainers.image.revision="${GIT_COMMIT}"

ENTRYPOINT ["dotnet", "ExpenseHub.Worker.dll"]
