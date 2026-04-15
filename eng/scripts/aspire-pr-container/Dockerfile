FROM mcr.microsoft.com/dotnet/sdk:10.0

ARG DEBIAN_FRONTEND=noninteractive

ENV DOTNET_NOLOGO=1 \
    NUGET_XMLDOC_MODE=skip \
    PATH=/workspace/.aspire/bin:$PATH

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        docker.io \
        gh \
        git \
        unzip \
    && rm -rf /var/lib/apt/lists/*

COPY entrypoint.sh /usr/local/bin/aspire-pr-entrypoint.sh
RUN chmod +x /usr/local/bin/aspire-pr-entrypoint.sh

WORKDIR /workspace

ENTRYPOINT ["/usr/local/bin/aspire-pr-entrypoint.sh"]
CMD ["bash"]
