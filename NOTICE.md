# Independent API client

LM Experiment Workbench is original MIT software. It is not LM Studio, an official LM Studio product, a binary patch or a model/runtime extraction. LM Studio marks belong to their owners; no endorsement is implied.

This client implements documented native REST API calls. LM Studio and a model must be independently installed/configured by the user. No model, runtime engine or LM Studio binary is distributed. Current validation used only an owned synthetic loopback HTTP server, not LM Studio inference.

The Windows distribution includes Microsoft's .NET runtime/Windows Forms. Microsoft license and third-party notices are retained; the versioned runtime license is in licenses/DOTNET-LICENSE.txt. No additional NuGet package is used beyond SDK framework/runtime assets.

Run bundles may contain prompts, model outputs, tool arguments/results and model metadata. Treat exports as sensitive research data. Explicit API tokens are transient and excluded from request/header records; known credential/error fields are redacted. Arbitrary undisclosed secrets in prompts or encoded backend responses cannot be guaranteed detected.
