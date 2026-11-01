# Source-first API investigation

## Existing capabilities

The official [native API overview](https://lmstudio.ai/docs/developer/rest) identifies v1 as the recommended route introduced with LM Studio0.4.0. Load echo, model metadata, chat/MCP response records and statistics are existing API features, not reverse-engineered discoveries.

The official documentation repository was resolved to commit **8c7159be3ffe364af10aa8ae6f5a7570e9402089** during implementation. Version-pinned contracts:

- [Load](https://github.com/lmstudio-ai/docs/blob/8c7159be3ffe364af10aa8ae6f5a7570e9402089/1_developer/2_rest/load.md): requested options and optional final load-config echo.
- [Chat](https://github.com/lmstudio-ai/docs/blob/8c7159be3ffe364af10aa8ae6f5a7570e9402089/1_developer/2_rest/chat.md): native input/integrations, response instance, output and statistics.
- [Models](https://github.com/lmstudio-ai/docs/blob/8c7159be3ffe364af10aa8ae6f5a7570e9402089/1_developer/2_rest/list.md): metadata and loaded instances.

Docs describe a contract, not proof that an installed version behaves identically. This project did not inspect installed LM Studio, private configuration, models or an existing server. It distributed no proprietary code or weights.

## Independent implementation

`Contracts.cs` validates loopback-only endpoints/specifications, constructs canonical exact request bodies and redacts response credentials before persistence. Requests use no proxy, cookies or automatic redirects. `Runner.cs` calls the documented sequence with HttpClient and records requested versus reported settings, terminal states, HTTP/error evidence and unknown identities. No API token is serialized into the specification or headers log.

`BundleStore.cs` publishes create-new SHA256-committed artifacts; verifies exact membership; exports exact files; compares requested leaf factors; and blocks incomplete/mismatched/tool replay. These are client evidence policies, not model/runtime reproducibility guarantees.

`Workbench.cs` provides a real Windows Forms experiment UI. Its guarded replay is a new explicit load/chat attempt with unchanged requested controls, not restoration of engine/GPU/random state. Closing during a run requests cancellation and waits for local finalization. `StubServer.cs` is a bounded owned loopback HTTP fixture. It never loads weights or executes a tool; its metadata/output/statistics are synthetic.

## Cancellation finding

A real HttpClient timeout closes the client wait while the synthetic backend task can continue and finish. The test checks this explicitly. Neither timeout nor CancellationToken cancellation is treated as proof of backend cancellation. Network/protocol failure after a load/chat attempt also leaves backend completion unknown.

## REA boundary

This is an API-first companion motivated by the broader REA candidate investigation. It is not a binary patch, extracted inference runtime or claimed successful LM Studio reverse-engineering trace. The [studied REA README revision](https://github.com/morluto/rea/blob/6e9ee97a69cb16c7150d8b550e705cba055ee4f0/README.md) supplies context only. API/public-source documentation was sufficient for the implemented client; no proprietary analysis was needed or invented.

## Evidence limits

Actual native render, exact payload hashes, HTTP behavior and bundled/extracted execution are testable here. Real model output, backend version parity, applied sampler internals, file fingerprints, tool schemas/code, GPU/runtime identity and causal benchmark interpretation remain unverified/unknown. See `docs/validation.json` and the public synthetic fixtures.
