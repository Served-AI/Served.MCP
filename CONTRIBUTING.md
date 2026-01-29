# Contributing to Served.MCP

Thank you for your interest in contributing to Served.MCP!

## Getting Started

1. Fork the repository
2. Clone your fork: `git clone https://github.com/YOUR_USERNAME/served-mcp.git`
3. Create a branch: `git checkout -b feature/your-feature`
4. Make your changes
5. Test your changes
6. Commit: `git commit -m "Add your feature"`
7. Push: `git push origin feature/your-feature`
8. Create a Pull Request

## Development Setup

```bash
# Clone
git clone https://github.com/Served-AI/Served.MCP.git
cd served-mcp

# Build
dotnet build

# Run locally
dotnet run
```

## Adding Custom MCP Tools

1. Create a new tool file in `tools/`
2. Implement the MCP tool interface
3. Register in the tool registry
4. Add documentation in `tools/mcp/`

## Code Style

- Follow C# naming conventions
- Use async/await for all API calls
- Add XML documentation for public APIs
- Document tools in unified format

## Pull Request Guidelines

- Keep PRs focused on a single feature or fix
- Update documentation if needed
- Ensure build passes
- Update README if adding new tools

## Questions?

- Open an issue on GitHub
- Join our Discord: https://discord.gg/unifiedhq

## License

By contributing, you agree that your contributions will be licensed under the MIT License.
