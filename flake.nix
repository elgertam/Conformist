{
  description = "Conformist - C# library for HTTP RFC compliance testing";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    flake-utils.url = "github:numtide/flake-utils";
  };

  outputs = { self, nixpkgs, flake-utils }:
    flake-utils.lib.eachDefaultSystem (system:
      let
        pkgs = nixpkgs.legacyPackages.${system};

        # .NET SDK version - using .NET 8 as it's LTS and matches modern development
        dotnetSdk = pkgs.dotnet-sdk_8;

        # Runtime packages for different scenarios
        dotnetRuntime = pkgs.dotnet-runtime_8;
        dotnetAspNetCore = pkgs.dotnet-aspnetcore_8;

      in
      {
        # Development shell with all necessary tools
        devShells.default = pkgs.mkShell {
          name = "dotnet-dev-shell";

          buildInputs = with pkgs; [
            # .NET SDK and runtimes
            dotnetSdk
            dotnetRuntime
            dotnetAspNetCore

            # Development tools
            git
            curl
            jq

            # HTTP testing tools
            httpie

            # Database tools (since library works with EF Core)
            sqlite
            postgresql

            # Optional: editors/IDEs (uncomment if needed)
            # vscode
            # jetbrains.rider
          ];

          shellHook = ''
            echo "🚀 Conformist Development Environment"
            echo "📦 .NET SDK Version: $(dotnet --version)"
            echo "🔧 Available commands:"
            echo "   dotnet restore    - Restore NuGet packages"
            echo "   dotnet build      - Build the library"
            echo "   dotnet test       - Run tests"
            echo "   dotnet pack       - Create NuGet package"
            echo ""

            # Set up .NET environment variables
            export DOTNET_CLI_TELEMETRY_OPTOUT=1
            export DOTNET_NOLOGO=1
            export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

            # Ensure dotnet tools are in PATH
            export PATH="$HOME/.dotnet/tools:$PATH"

            # Set up NuGet configuration for better performance
            export NUGET_PACKAGES="$(pwd)/.nuget/packages"

            echo "✅ Environment ready! Run 'dotnet restore' to get started."
          '';

          # Environment variables for development
          DOTNET_ROOT = "${dotnetSdk}";
          DOTNET_CLI_HOME = ".dotnet-home";
        };

        # Package definition for the library itself
        packages.default = pkgs.buildDotnetModule {
          pname = "Conformist";
          version = "1.0.0";

          src = ./.;

          projectFile = "Conformist.csproj";
          nugetDeps = ./deps.nix; # Will need to be generated

          dotnet-sdk = dotnetSdk;
          dotnet-runtime = dotnetRuntime;

          meta = with pkgs.lib; {
            description = "A comprehensive C# library for property-based testing of WebAPI endpoints for HTTP RFC compliance";
            homepage = "https://github.com/your-org/Conformist";
            license = licenses.mit;
            maintainers = [ "Your Name" ];
            platforms = platforms.linux ++ platforms.darwin;
          };
        };

        # Additional packages for different scenarios
        packages.examples = pkgs.stdenv.mkDerivation {
          name = "httprfc-examples";
          src = ./Examples;

          buildInputs = [ dotnetSdk ];

          buildPhase = ''
            dotnet build Examples/
          '';

          installPhase = ''
            mkdir -p $out/share/examples
            cp -r Examples/* $out/share/examples/
          '';
        };

        # Apps for easy execution
        apps = {
          # Run the development environment
          default = flake-utils.lib.mkApp {
            drv = pkgs.writeShellScriptBin "httprfc-dev" ''
              echo "Starting HttpRfcConformance.Testing development environment..."
              ${dotnetSdk}/bin/dotnet --version
            '';
          };

          # Build the library
          build = flake-utils.lib.mkApp {
            drv = pkgs.writeShellScriptBin "httprfc-build" ''
              ${dotnetSdk}/bin/dotnet build
            '';
          };

          # Run tests
          test = flake-utils.lib.mkApp {
            drv = pkgs.writeShellScriptBin "httprfc-test" ''
              ${dotnetSdk}/bin/dotnet test
            '';
          };

          # Create NuGet package
          pack = flake-utils.lib.mkApp {
            drv = pkgs.writeShellScriptBin "httprfc-pack" ''
              ${dotnetSdk}/bin/dotnet pack --configuration Release --output ./nupkg/
            '';
          };
        };

        # Formatter for the flake
        formatter = pkgs.alejandra;
      }
    );
}
