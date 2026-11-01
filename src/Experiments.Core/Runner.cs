// SPDX-License-Identifier: MIT
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace Experiments;
public sealed class ExperimentRunner
{
 public async Task<BundleResult> RunAsync(ExperimentSpec requested,string parent,string credential="",CancellationToken cancellation=default,bool requireMatchingLoadEcho=false)
 {
  var spec=Data.Validate(requested);byte[] specBytes=Data.Bytes(spec);
  if(Data.ContainsCredential(spec,credential))throw new ArgumentException("Credential also appears in an experiment field. Remove it before recording an exact request.");
  var files=new Dictionary<string,(byte[],string)>{{"spec.json",(specBytes,"requested-experiment")}};var events=new List<LifecycleEvent>();var checks=new List<ConfigCheck>();string state="failed",detail="",instance="",responseInstance="";bool unknown=false;
  void Event(string stage,string status,string text)=>events.Add(new(DateTimeOffset.UtcNow,stage,status,text));
  using var handler=new SocketsHttpHandler{AllowAutoRedirect=false,UseProxy=false,UseCookies=false,ConnectTimeout=TimeSpan.FromSeconds(5)};
  using var client=new HttpClient(handler){BaseAddress=new Uri(spec.Endpoint),Timeout=Timeout.InfiniteTimeSpan};
  using var timeout=new CancellationTokenSource(spec.TimeoutMilliseconds);using var linked=CancellationTokenSource.CreateLinkedTokenSource(timeout.Token,cancellation);
  async Task<JsonObject> Send(string stage,HttpMethod method,string path,JsonObject? payload=null)
  {
   Event(stage,"request-start",method+" "+path+"; Authorization header intentionally omitted from records.");
   using var message=new HttpRequestMessage(method,path);if(credential!="")message.Headers.Authorization=new AuthenticationHeaderValue("Bearer",credential);
   if(payload is not null){byte[] body=Data.Canonical(payload);files[stage+"-request.json"]=(body,"exact-request-body; credential header omitted");message.Content=new ByteArrayContent(body);message.Content.Headers.ContentType=new MediaTypeHeaderValue("application/json");}
   using var response=await client.SendAsync(message,HttpCompletionOption.ResponseHeadersRead,linked.Token);Event(stage,"headers-received","HTTP "+(int)response.StatusCode);
   await using var stream=await response.Content.ReadAsStreamAsync(linked.Token);using var buffer=new MemoryStream();byte[] chunk=new byte[8192];int count;
   while((count=await stream.ReadAsync(chunk,linked.Token))!=0){if(buffer.Length+count>BundleStore.FileBudget)throw new InvalidDataException("Response exceeded2MiB; body not persisted.");buffer.Write(chunk,0,count);}
   var safe=Data.Redact(buffer.ToArray(),credential);files[stage+"-response.json"]=(safe.Bytes,safe.Redacted?"redacted-response; exact raw bytes intentionally not retained":"exact-response-body");
   if(!response.IsSuccessStatusCode)throw new HttpRequestException("Server returnedHTTP "+(int)response.StatusCode+". Redacted response body is in the bundle.",null,response.StatusCode);
   Event(stage,"response-recorded",safe.Redacted?"Credentials redacted before persistence.":"Exact response bytes recorded.");
   return JsonNode.Parse(safe.Bytes) as JsonObject??throw new InvalidDataException("Expected a JSON object response.");
  }
  Event("run","validated","Loopback-only endpoint; no automatic redirects/proxy. Backend identity remains unverified.");
  try{
   var models=await Send("models",HttpMethod.Get,"api/v1/models");var list=models["models"] as JsonArray??throw new InvalidDataException("No models array in response.");if(!list.Any(m=>m?["key"]?.GetValue<string>()==spec.ModelKey))throw new InvalidDataException("Requested exact model key absent from server list.");
   var load=await Send("load",HttpMethod.Post,"api/v1/models/load",Data.LoadPayload(spec));instance=load["instance_id"]?.GetValue<string>()??"";if(instance==""||load["status"]?.GetValue<string>()!="loaded")throw new InvalidDataException("Load response lacks a loaded instance identifier/status.");
   var echo=load["load_config"] as JsonObject;var loadRequest=Data.LoadPayload(spec);
   foreach(string key in new[]{"context_length","flash_attention","eval_batch_size"}){string want=loadRequest[key]!.ToJsonString(),applied=echo?[key]?.ToJsonString()??"unknown (not echoed)";bool same=applied==want||(key!="flash_attention"&&double.TryParse(applied,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double actual)&&double.TryParse(want,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double expected)&&actual==expected);checks.Add(new(key,want,applied,same?"match":echo?[key] is null?"unknown":"different"));}
   Event("load","applied-settings-recorded","Echo is a server claim, not measured hardware/model identity.");
   if(requireMatchingLoadEcho&&checks.Any(c=>c.Verdict!="match"))throw new InvalidDataException("Configuration replay aborted before chat: current server load echo is missing or differs from requested settings. The model may remain loaded.");
   var chat=await Send("chat",HttpMethod.Post,"api/v1/chat",Data.ChatPayload(spec,instance));responseInstance=chat["model_instance_id"]?.GetValue<string>()??"";
   if(responseInstance!=instance)throw new InvalidDataException("Chat model_instance_id differs from loaded instance; response retained without a successful replay claim.");
   if(chat["output"] is not JsonArray||chat["stats"] is not JsonObject)throw new InvalidDataException("Chat response lacks documented output/stats fields.");
   state="completed";detail="Documented HTTP workflow completed. No model/runtime parity or deterministic-output claim.";Event("run","completed",detail);
  }catch(OperationCanceledException){state=cancellation.IsCancellationRequested?"client_canceled":"client_timeout";detail="Client stopped waiting. The backend may continue loading, generating or executing tools; backend completion is unknown.";unknown=true;Event("run",state,detail);}
  catch(Exception e)when(e is HttpRequestException or IOException or InvalidDataException or JsonException or InvalidOperationException or FormatException){detail=e.Message;Event("run","failed",detail);}
  if(state!="completed"&&events.Any(e=>e.State=="request-start"&&e.Stage is "load" or "chat"))unknown=true;
  var identity=new IdentityRecord(spec.ModelKey,instance==""?null:instance,responseInstance==""?null:responseInstance,"unknown — no model-file digest established","unknown — no engine build established","unknown — integration descriptors do not pin tool schemas or code","API identifiers/metadata only; no installed model or private app file was read");
  var outcome=new Outcome(state,detail,unknown,identity,checks.ToArray(),"unknown — native chat does not echo a complete applied sampler/runtime configuration",events.ToArray());
  var safeOutcome=Data.Redact(Data.Bytes(outcome),credential);outcome=JsonSerializer.Deserialize<Outcome>(safeOutcome.Bytes,Data.Json)!;files["outcome.json"]=(safeOutcome.Bytes,"derived lifecycle/configuration/identity record");
  return BundleStore.Write(parent,files,outcome);
 }
}
