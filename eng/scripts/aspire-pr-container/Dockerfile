FROM ubuntu:24.04

ARG DEBIAN_FRONTEND=noninteractive

ENV PATH=/workspace/.aspire/bin:$PATH

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
