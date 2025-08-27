# Fallback shell.nix for systems without flakes enabled
{ pkgs ? import <nixpkgs> {} }:

pkgs.mkShell {
  name = "httprfc-conformance-dev";
  
  buildInputs = with pkgs; [
    # .NET development
    dotnet-sdk_8
    dotnet-runtime_8
    dotnet-aspnetcore_8
    
    # Development tools
    git
    curl
    jq
    
    # Database tools
    sqlite
    postgresql
  ];

  shellHook = ''
    echo "🚀 Conformist Development Environment (legacy mode)"
    echo "📦 .NET SDK Version: $(dotnet --version)"
    
    # Set up .NET environment
    export DOTNET_CLI_TELEMETRY_OPTOUT=1
    export DOTNET_NOLOGO=1
    export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
    export NUGET_PACKAGES="$PWD/.nuget/packages"
    
    echo "✅ Environment ready! Run 'dotnet restore' to get started."
  '';
}