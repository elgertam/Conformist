# Conformist Examples

This directory contains examples of how to use the Conformist library.

## Files

- **SimpleExample.cs** - Basic examples showing core functionality
- **SampleWebApi/** - A minimal ASP.NET Core Web API project used for demonstration

## Usage

1. Replace `Program` in the examples with your actual web application's Program class
2. Replace `BlogContext` with your actual DbContext
3. Configure the test builder as needed for your API

## Running the Examples

```bash
# Build the examples
dotnet build

# Run the tests
dotnet test
```

## Key Features Demonstrated

- Basic RFC compliance testing
- State tracking with Entity Framework
- Endpoint filtering
- Custom property definitions
- Report generation