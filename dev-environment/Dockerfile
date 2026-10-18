FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /workspace
RUN dotnet tool install --global dotnet-ef --version 10.0.12
ENV PATH=/root/.dotnet/tools:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin
COPY . /workspace