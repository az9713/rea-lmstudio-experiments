// SPDX-License-Identifier: MIT
// Controlled synthetic HTTP server. It never loads a model or executes a tool.
using System.Net;using System.Net.Sockets;using System.Text;using System.Text.Json.Nodes;using Experiments;
namespace Experiments.Tests;
public sealed class StubServer:IAsyncDisposable
{
 readonly TcpListener listener=new(IPAddress.Loopback,0);readonly CancellationTokenSource stop=new();readonly List<Task> clients=[];readonly Task loop;
 public string Mode{get;set;}="good";public int Requests;public int BackendFinishes;public string? LastAuthorization;public byte[]? LastChatRequest;
 public string Endpoint{get;}
 public StubServer(){listener.Start();Endpoint="http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port+"/";loop=Accept();}
 async Task Accept(){try{while(!stop.IsCancellationRequested){var client=await listener.AcceptTcpClientAsync(stop.Token);lock(clients)clients.Add(Handle(client));}}catch(OperationCanceledException){}catch(SocketException)when(stop.IsCancellationRequested){}}
 async Task Handle(TcpClient client)
 {
  using(client)try{await using var stream=client.GetStream();var header=new List<byte>();byte[] one=new byte[1];while(header.Count<16384){if(await stream.ReadAsync(one,stop.Token)==0)return;header.Add(one[0]);if(header.Count>=4&&Encoding.ASCII.GetString(header.TakeLast(4).ToArray())=="\r\n\r\n")break;}
   string text=Encoding.ASCII.GetString(header.ToArray());var lines=text.Split("\r\n");string path=lines[0].Split(' ')[1];int length=0;
   foreach(string line in lines.Skip(1)){if(line.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase))length=int.Parse(line[15..].Trim(),System.Globalization.CultureInfo.InvariantCulture);if(line.StartsWith("Authorization:",StringComparison.OrdinalIgnoreCase))LastAuthorization=line[14..].Trim();}
   if(length>2*1024*1024)return;byte[] body=new byte[length];await stream.ReadExactlyAsync(body,stop.Token);Interlocked.Increment(ref Requests);
   int code=200;string response;string extra="";
   if(Mode=="redirect"){code=302;extra="Location: "+Endpoint+"never-contact\r\n";response="{}";}
   else if(Mode=="error"){code=401;response=new JsonObject{["error"]="synthetic rejection "+LastAuthorization,["api_key"]="synthetic-private-field"}.ToJsonString();}
   else if(Mode=="malformed")response="{broken JSON";
   else if(Mode=="oversize")response=new string('x',2*1024*1024+1);
   else if(path=="/api/v1/models")response="{\"models\":[{\"type\":\"llm\",\"key\":\"synthetic/model\",\"publisher\":\"controlled-stub\",\"format\":\"gguf\",\"size_bytes\":0,\"quantization\":{\"name\":\"synthetic\",\"bits_per_weight\":4},\"loaded_instances\":[]}]}";
   else if(path=="/api/v1/models/load"){
    var request=JsonNode.Parse(body)!;var obj=new JsonObject{["type"]="llm",["instance_id"]="synthetic-instance",["status"]="loaded",["load_time_seconds"]=0};
    if(Mode!="missing-echo"){obj["load_config"]=new JsonObject{["context_length"]=Mode=="echo-mismatch"?1024:request["context_length"]!.GetValue<int>(),["flash_attention"]=request["flash_attention"]!.GetValue<bool>(),["eval_batch_size"]=request["eval_batch_size"]!.GetValue<int>()};if(Mode=="echo-float")obj["load_config"]!["context_length"]=JsonNode.Parse("4096.0");}response=obj.ToJsonString();
   }else if(path=="/api/v1/chat"){
    LastChatRequest=body;if(Mode=="slow")await Task.Delay(500,stop.Token);Interlocked.Increment(ref BackendFinishes);
    var outputs=new JsonArray();if((JsonNode.Parse(body)?["integrations"] as JsonArray)?.Count>0)outputs.Add(new JsonObject{["type"]="tool_call",["tool"]="fixture",["arguments"]=new JsonObject{["value"]=1},["output"]="synthetic fixture result; no tool executed",["provider_info"]=new JsonObject{["type"]="plugin",["plugin_id"]="mcp/synthetic"}});outputs.Add(new JsonObject{["type"]="message",["content"]="SYNTHETIC STUB OUTPUT — no inference performed"});
    response=new JsonObject{["model_instance_id"]=Mode=="instance-mismatch"?"other-instance":"synthetic-instance",["output"]=outputs,["stats"]=new JsonObject{["input_tokens"]=12,["total_output_tokens"]=10,["tokens_per_second"]=50,["time_to_first_token_seconds"]=0.01}}.ToJsonString();
   }else{code=404;response="{}";}
   byte[] bytes=Encoding.UTF8.GetBytes(response);byte[] responseHeader=Encoding.ASCII.GetBytes($"HTTP/1.1 {code} Synthetic\r\nContent-Type: application/json\r\nContent-Length: {bytes.Length}\r\n{extra}Connection: close\r\n\r\n");await stream.WriteAsync(responseHeader,stop.Token);await stream.WriteAsync(bytes,stop.Token);
  }catch(Exception e)when(e is IOException or OperationCanceledException or SocketException){}
 }
 public async ValueTask DisposeAsync(){stop.Cancel();listener.Stop();await loop;Task[] pending;lock(clients)pending=clients.ToArray();await Task.WhenAll(pending);stop.Dispose();}
}
