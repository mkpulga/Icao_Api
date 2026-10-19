# Stage 1: Build application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env
WORKDIR /app

# Copy csproj and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy all source code and build production files
COPY . ./
RUN dotnet publish -c Release -o out

# Stage 2: Create final container environment
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Crucial: Install libgdiplus for System.Drawing.Bitmap support on Linux
RUN apt-get update && apt-get install -y \
    libgdiplus \
    libc6-dev \
    && rm -rf /var/lib/apt/lists/*

# Copy compiled .NET app
COPY --from=build-env /app/out .

# CRUCIAL STEPS FOR BIOMETRIC LIBRARIES:
# 1. Ensure your 'onejkit.lic' license exists in your project root so it copies here:
# COPY onejkit.lic .

# 2. Copy the LINUX versions of Innovatrics native libraries (.so files) 
# into the application execution directory so .NET can resolve P/Invokes:
# COPY ./libs/linux/*.so ./

# Expose the API port defined in your worker service code
EXPOSE 7006

ENTRYPOINT ["dotnet", "obsIcaoApi.dll"]
