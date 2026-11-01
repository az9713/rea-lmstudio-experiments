// SPDX-License-Identifier: MIT
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Experiments;
public sealed record ExperimentSpec(string Endpoint,string ModelKey,string Prompt,string SystemPrompt,int ContextLength=4096,bool FlashAttention=true,int EvalBatchSize=512,double Temperature=0.2,double TopP=0.95,int MaxOutputTokens=256,string IntegrationsJson="[]",int TimeoutMilliseconds=30000,string BackendLabel="unverified LM Studio-compatible endpoint");
public sealed record LifecycleEvent(DateTimeOffset Time,string Stage,string State,string Detail);
public sealed record ConfigCheck(string Key,string Requested,string Applied,string Verdict);
public sealed record IdentityRecord(string RequestedModelKey,string? LoadedInstance,string? ResponseInstance,string ModelFileSha256,string RuntimeEngineVersion,string ToolDefinitionAndCodeSha256,string Source);
public sealed record Outcome(string State,string Detail,bool BackendCompletionUnknown,IdentityRecord Identity,ConfigCheck[] LoadChecks,string AppliedInferenceConfiguration,LifecycleEvent[] Events);
public sealed record Artifact(string Name,long Bytes,string Sha256,string Representation);
public sealed record Manifest(string Schema,string Id,DateTimeOffset CreatedUtc,string Producer,Artifact[] Artifacts);
public sealed record BundleResult(string Directory,string Id,string Digest,Outcome Outcome);
public sealed record Verification(bool Valid,string? Digest,string[] Issues);
public sealed record FactorChange(string Path,string Before,string After);
public sealed record Comparison(bool Valid,bool ExactlyOneRequestedFactorChanged,FactorChange[] RequestedChanges,string[] ObservedDifferences,string CausalAttribution);
public sealed record ReplayDecision(bool ConfigurationReplayAllowed,string[] Blockers,string[] Warnings);

public static class Data
{
 public static readonly JsonSerializerOptions Json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,WriteIndented=true,MaxDepth=40,UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow};
 public static string Digest(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
 public static byte[] Bytes<T>(T obj)=>JsonSerializer.SerializeToUtf8Bytes(obj,Json);
 public static JsonNode CanonicalNode(JsonNode node)=>node switch{JsonObject o=>new JsonObject(o.OrderBy(k=>k.Key,StringComparer.Ordinal).Select(k=>KeyValuePair.Create(k.Key,k.Value is null?null:CanonicalNode(k.Value))).ToArray()),JsonArray a=>new JsonArray(a.Select(v=>v is null?null:CanonicalNode(v)).ToArray()),_=>node.DeepClone()};
 public static byte[] Canonical(JsonNode node)=>Encoding.UTF8.GetBytes(CanonicalNode(node).ToJsonString(new JsonSerializerOptions{WriteIndented=false,MaxDepth=40}));
 public static Uri Endpoint(string endpoint)
 {
  if(!Uri.TryCreate(endpoint,UriKind.Absolute,out var uri)||uri.Scheme!="http"||uri.UserInfo!=""||uri.Query!=""||uri.Fragment!=""||uri.AbsolutePath!="/")throw new ArgumentException("Use an http loopback base URL only, with no user info, path, query or fragment.");
  string host=uri.Host.Trim('[',']');if(host=="localhost")host="127.0.0.1";
  if(!IPAddress.TryParse(host,out var address)||!IPAddress.IsLoopback(address))throw new ArgumentException("Only literal loopback addresses or localhost are allowed. No remote/DNS target is contacted.");
  return new UriBuilder(uri){Host=host}.Uri;
 }
 public static ExperimentSpec Validate(ExperimentSpec s)
 {
  var uri=Endpoint(s.Endpoint);
  if(s.BackendLabel.Length>200||s.BackendLabel.Any(char.IsControl))throw new ArgumentException("Backend label is user-declared metadata, limited to200 printable characters.");
  if(string.IsNullOrWhiteSpace(s.ModelKey)||s.ModelKey.Length>200||s.ModelKey.Any(char.IsControl))throw new ArgumentException("Enter a model key of1–200 printable characters.");
  if(string.IsNullOrWhiteSpace(s.Prompt)||s.Prompt.Length>32000||s.SystemPrompt.Length>16000)throw new ArgumentException("Prompt must have1–32000 characters; system prompt≤16000.");
  if(s.ContextLength is <128 or >131072||s.EvalBatchSize is <1 or >4096||s.MaxOutputTokens is <1 or >8192||s.TimeoutMilliseconds is <50 or >600000||!double.IsFinite(s.Temperature)||s.Temperature is <0 or >1||!double.IsFinite(s.TopP)||s.TopP is <=0 or >1)throw new ArgumentException("Configuration exceeds the workbench's declared bounds.");
  if(s.IntegrationsJson.Length>16000)throw new ArgumentException("Integration declaration exceeds16KiB.");
  var integrations=JsonNode.Parse(s.IntegrationsJson) as JsonArray??throw new ArgumentException("Integrations must be a JSON array.");
  if(integrations.Count>8)throw new ArgumentException("At most8 integrations.");
  foreach(var value in integrations){if(value is not JsonObject plugin||plugin["type"]?.GetValue<string>()!="plugin"||plugin["id"] is not JsonValue id||!id.GetValue<string>().StartsWith("mcp/",StringComparison.Ordinal)||plugin["allowed_tools"] is not JsonArray tools||tools.Count is <1 or >30||tools.Any(t=>t is not JsonValue||string.IsNullOrWhiteSpace(t.GetValue<string>()))||plugin.Any(k=>k.Key is not "type" and not "id" and not "allowed_tools"))throw new ArgumentException("Only explicit plugin integrations with type,id and nonempty allowed_tools are supported. No remote MCP URL/credential fields.");}
  return s with{Endpoint=uri.AbsoluteUri,IntegrationsJson=Encoding.UTF8.GetString(Canonical(integrations))};
 }
 public static JsonObject LoadPayload(ExperimentSpec s)=>new(){["model"]=s.ModelKey,["context_length"]=s.ContextLength,["flash_attention"]=s.FlashAttention,["eval_batch_size"]=s.EvalBatchSize,["echo_load_config"]=true};
 public static JsonObject ChatPayload(ExperimentSpec s,string instance)=>new(){["model"]=instance,["input"]=s.Prompt,["system_prompt"]=s.SystemPrompt,["temperature"]=s.Temperature,["top_p"]=s.TopP,["max_output_tokens"]=s.MaxOutputTokens,["integrations"]=JsonNode.Parse(s.IntegrationsJson),["store"]=false,["stream"]=false};
 public static bool ContainsCredential(ExperimentSpec s,string credential){if(credential=="")return false;bool Walk(JsonNode? n)=>n switch{JsonObject o=>o.Any(k=>k.Key.Contains(credential,StringComparison.Ordinal)||Walk(k.Value)),JsonArray a=>a.Any(Walk),JsonValue v=>v.TryGetValue<string>(out var text)&&text.Contains(credential,StringComparison.Ordinal),_=>false};return new[]{s.Endpoint,s.ModelKey,s.Prompt,s.SystemPrompt,s.BackendLabel}.Any(t=>t.Contains(credential,StringComparison.Ordinal))||Walk(JsonNode.Parse(s.IntegrationsJson));}
 public static (byte[] Bytes,bool Redacted) Redact(byte[] original,string credential)
 {
  string text=Encoding.UTF8.GetString(original);bool changed=false;
  string Scrub(string value){if(!string.IsNullOrEmpty(credential)&&value.Contains(credential,StringComparison.Ordinal)){changed=true;value=value.Replace(credential,"[REDACTED]",StringComparison.Ordinal);}string clean=Regex.Replace(value,@"(?i)Bearer\s+[A-Za-z0-9._~+/=-]+","Bearer [REDACTED]");if(clean!=value)changed=true;return clean;}
  JsonNode? Walk(JsonNode? node){if(node is JsonObject obj){var copy=new JsonObject();int i=0;foreach(var pair in obj){string key=Scrub(pair.Key);if(copy.ContainsKey(key))key="redacted-key-"+(i++);if(new[]{"authorization","api_key","access_token","refresh_token","password","secret"}.Contains(pair.Key.ToLowerInvariant())){copy[key]="[REDACTED]";changed=true;}else copy[key]=Walk(pair.Value);}return copy;}if(node is JsonArray arr)return new JsonArray(arr.Select(Walk).ToArray());if(node is JsonValue val&&val.TryGetValue<string>(out var str))return JsonValue.Create(Scrub(str));return node?.DeepClone();}
  try{var node=Walk(JsonNode.Parse(text,documentOptions:new JsonDocumentOptions{MaxDepth=40}));if(changed)return(Encoding.UTF8.GetBytes(node?.ToJsonString(Json)??"null"),true);return(original,false);}catch(JsonException){return(Encoding.UTF8.GetBytes("{\"body_omitted\":true,\"reason\":\"Non-JSON or unsupported-depth response omitted before persistence\"}"),true);}
 }
}
