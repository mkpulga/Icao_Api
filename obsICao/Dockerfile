# Stage 1: Build the app normally on the .NET SDK
FROM ://microsoft.com AS build-env
WORKDIR /app
COPY *.csproj ./
RUN dotnet restore obsIcaoApi.csproj
COPY . ./
RUN dotnet publish -c Release -r win-x64 --self-contained false -o out obsIcaoApi.csproj

# Stage 2: Create a Linux image equipped with Wine to execute Windows .dlls
FROM ://microsoft.com
WORKDIR /app

# Install Wine and dependencies inside the Linux Container
RUN dpkg --add-architecture i386 && \
    apt-get update && apt-get install -y \
    wine \
    wine32 \
    wine64 \
    libgdiplus \
    && rm -rf /var/lib/apt/lists/*

# Copy your built application 
COPY --from=build-env /app/out .

# Initialize Wine directory for headless execution
RUN wineboot --init

# Set up Wine execution variables
ENV WINEPREFIX=/root/.wine
ENV WINEDEBUG=-all

EXPOSE 7006

# Run the .NET application through the Wine layer to resolve Windows P/Invokes
ENTRYPOINT ["wine", "obsIcaoApi.exe"]
