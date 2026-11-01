// SPDX-License-Identifier: MIT
using Experiments;using System.Globalization;using System.Text.Json;
namespace Experiments.App;
public sealed class Workbench:Form
{
 readonly TextBox endpoint=new(){Text="http://127.0.0.1:1234/"},model=new(){Text="enter-exact-model-key"},credential=new(){UseSystemPasswordChar=true},prompt=new(){Multiline=true,Text="Explain one controlled experiment."},system=new(){Text="Be concise and distinguish evidence from assumptions."},integrations=new(){Multiline=true,Text="[]"},outputFolder=new(){Text=Path.Combine(AppContext.BaseDirectory,"runs")};
 readonly NumericUpDown context=Number(128,131072,4096),batch=Number(1,4096,512),temperature=Number(0,1,.2m,2),topP=Number(.01m,1,.95m,2),maxTokens=Number(1,8192,256),timeout=Number(50,600000,30000);
 readonly CheckBox flash=new(){Text="Flash attention requested",Checked=true,AutoSize=true};readonly TextBox report=TextOutput(),payload=TextOutput(),timeline=TextOutput();
 readonly Label status=new(){Dock=DockStyle.Fill,Text="No server contacted. Configure the loopback endpoint and model, then explicitly run.",ForeColor=Color.LightSkyBlue};
 readonly Button runButton=new(){Text="Load model + run",AutoSize=true},cancelButton=new(){Text="Cancel client wait",AutoSize=true,Enabled=false};
 CancellationTokenSource? cancellation;BundleResult? current;string? currentDirectory;bool closePending;
 static readonly Color Bg=Color.FromArgb(10,20,31),Panel=Color.FromArgb(18,36,51),Ink=Color.FromArgb(231,241,249),Blue=Color.FromArgb(112,211,246),Gold=Color.FromArgb(244,197,126);
 static NumericUpDown Number(decimal min,decimal max,decimal value,int decimals=0)=>new(){Minimum=min,Maximum=max,Value=value,DecimalPlaces=decimals,Increment=decimals>0?.05m:1,Dock=DockStyle.Fill};
 static TextBox TextOutput()=>new(){ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Both,Dock=DockStyle.Fill,WordWrap=false};
 public Workbench()
 {
  Text="LM Experiment Workbench — requested, reported, unknown";Width=1450;Height=1000;MinimumSize=new(1100,800);StartPosition=FormStartPosition.CenterScreen;BackColor=Bg;ForeColor=Ink;Font=new Font("Segoe UI",10);
  var outer=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=4,ColumnCount=1,Padding=new Padding(18)};outer.RowStyles.Add(new(SizeType.Absolute,52));outer.RowStyles.Add(new(SizeType.Absolute,65));outer.RowStyles.Add(new(SizeType.Percent,100));outer.RowStyles.Add(new(SizeType.Absolute,55));
  outer.Controls.Add(new Label{Text="LM EXPERIMENT WORKBENCH  /  requested · server-reported · unknown",ForeColor=Blue,Font=new Font(Font.FontFamily,17,FontStyle.Bold),Dock=DockStyle.Fill});outer.Controls.Add(status);
  var split=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};split.ColumnStyles.Add(new(SizeType.Percent,40));split.ColumnStyles.Add(new(SizeType.Percent,60));
  var left=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=15,AutoScroll=true,Padding=new Padding(0,0,18,0)};left.ColumnStyles.Add(new(SizeType.Absolute,145));left.ColumnStyles.Add(new(SizeType.Percent,100));
  void Field(string title,Control control,int height=35){int row=left.RowStyles.Count;left.RowStyles.Add(new(SizeType.Absolute,height));var label=new Label{Text=title,Dock=DockStyle.Fill,ForeColor=Blue,Font=new Font("Segoe UI",9)};left.Controls.Add(label,0,row);control.Dock=DockStyle.Fill;left.Controls.Add(control,1,row);}
  Field("Endpoint",endpoint);Field("Model key",model);Field("API token",credential);Field("Context / batch",Pair(context,batch));Field("Temperature / topP",Pair(temperature,topP));Field("Max tokens / timeout ms",Pair(maxTokens,timeout));Field("Load option",flash);Field("System prompt",system,50);Field("Prompt",prompt,110);Field("Plugin declarations",integrations,80);Field("Output parent",outputFolder,45);
  var controls=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};controls.Controls.AddRange([runButton,cancelButton,Button("Choose output…",ChooseOutput)]);Field("Explicit actions",controls,75);
  var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};var replay=Button("Replay same config",()=>{});replay.Click+=async(_,_)=>{try{if(currentDirectory is null)throw new InvalidOperationException("Open or run a bundle first.");var decision=BundleStore.ReplayGuard(currentDirectory,Data.Validate(Spec()));report.Text=JsonSerializer.Serialize(decision,Data.Json);if(decision.ConfigurationReplayAllowed)await Run(true);else status.Text="Replay blocked. Review the guard report; use Load model + run for a deliberately changed experiment.";}catch(Exception e){status.Text=e.Message;}};actions.Controls.AddRange([Button("Open bundle",Open),Button("Compare…",Compare),Button("Replay guard",Guard),replay,Button("Export…",Export)]);Field("Bundle tools",actions,90);
  var right=new TabControl{Dock=DockStyle.Fill};AddTab(right,"Run evidence / load echo",report);AddTab(right,"Stored payloads",payload);AddTab(right,"Lifecycle",timeline);split.Controls.Add(left);split.Controls.Add(right);outer.Controls.Add(split);
  outer.Controls.Add(new Label{Dock=DockStyle.Fill,ForeColor=Gold,Text="Independent API client. Run explicitly loads/generates; declared plugins may execute server-side tools. Unknown file/runtime identities prevent strong reproducibility claims. Tests use a controlled stub."});Controls.Add(outer);
  foreach(Control c in new Control[]{endpoint,model,credential,prompt,system,integrations,outputFolder,report,payload,timeline,context,batch,temperature,topP,maxTokens,timeout}){c.BackColor=Panel;c.ForeColor=Ink;if(c is TextBox box){box.BorderStyle=BorderStyle.FixedSingle;box.Font=new Font("Consolas",10);}}
  runButton.Click+=async(_,_)=>await Run();cancelButton.Click+=(_,_)=>{cancellation?.Cancel();status.Text="Client cancellation requested. Backend may continue; no backend cancellation is claimed.";};FormClosing+=async(_,e)=>{if(cancellation is null)return;e.Cancel=true;if(closePending)return;closePending=true;cancellation.Cancel();status.Text="Waiting for the canceled run to seal its local record before closing.";while(cancellation is not null)await Task.Delay(50);Close();};
 }
 static Control Pair(Control a,Control b){var p=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};p.ColumnStyles.Add(new(SizeType.Percent,50));p.ColumnStyles.Add(new(SizeType.Percent,50));p.Controls.Add(a);p.Controls.Add(b);return p;}
 static Button Button(string text,Action action){var b=new Button{Text=text,AutoSize=true,FlatStyle=FlatStyle.Flat,BackColor=Panel,ForeColor=Ink};b.Click+=(_,_)=>{try{action();}catch(Exception e){MessageBox.Show(e.Message,"Workbench",MessageBoxButtons.OK,MessageBoxIcon.Information);}};return b;}
 static void AddTab(TabControl tabs,string text,Control control){var tab=new TabPage(text){BackColor=Panel,ForeColor=Ink};tab.Controls.Add(control);tabs.TabPages.Add(tab);}
 ExperimentSpec Spec()=>new(endpoint.Text,model.Text,prompt.Text,system.Text,(int)context.Value,flash.Checked,(int)batch.Value,(double)temperature.Value,(double)topP.Value,(int)maxTokens.Value,integrations.Text,(int)timeout.Value);
 async Task Run(bool guardedReplay=false){if(cancellation is not null)return;try{var spec=Data.Validate(Spec());cancellation=new();runButton.Enabled=false;cancelButton.Enabled=true;status.Text="RUNNING / recording local HTTP evidence. Cancellation stops the client wait only.";var result=await new ExperimentRunner().RunAsync(spec,outputFolder.Text,credential.Text,cancellation.Token,guardedReplay);if(!IsDisposed&&!Disposing)ShowRun(result);}catch(Exception e){if(!IsDisposed&&!Disposing)status.Text=e.Message;}finally{cancellation?.Dispose();cancellation=null;if(!IsDisposed&&!Disposing){credential.Clear();runButton.Enabled=true;cancelButton.Enabled=false;}}}
 public void ShowRun(BundleResult result)
 {
  current=result;currentDirectory=result.Directory;
  var spec=Data.Validate(BundleStore.ReadArtifact<ExperimentSpec>(result.Directory,"spec.json"));
  endpoint.Text=spec.Endpoint;model.Text=spec.ModelKey;prompt.Text=spec.Prompt;system.Text=spec.SystemPrompt;
  context.Value=spec.ContextLength;batch.Value=spec.EvalBatchSize;flash.Checked=spec.FlashAttention;
  temperature.Value=(decimal)spec.Temperature;topP.Value=(decimal)spec.TopP;maxTokens.Value=spec.MaxOutputTokens;timeout.Value=spec.TimeoutMilliseconds;integrations.Text=spec.IntegrationsJson;
  status.Text=$"{result.Outcome.State.ToUpperInvariant()} / {spec.BackendLabel} / run{result.Id}. File and engine identities unknown.";
  report.Text=JsonSerializer.Serialize(new{status=result.Outcome.State,backendLabel=spec.BackendLabel,integrity=BundleStore.Verify(result.Directory,result.Digest),requestedVsReportedLoad=result.Outcome.LoadChecks,identities=result.Outcome.Identity,appliedInference=result.Outcome.AppliedInferenceConfiguration,detail=result.Outcome.Detail},Data.Json);
  report.SelectionStart=0;report.ScrollToCaret();
  var manifest=JsonSerializer.Deserialize<Manifest>(File.ReadAllBytes(Path.Combine(result.Directory,"manifest.json")),Data.Json)!;
  payload.Text=string.Join(Environment.NewLine+Environment.NewLine,manifest.Artifacts.Where(a=>a.Name.EndsWith("-request.json",StringComparison.Ordinal)||a.Name.EndsWith("-response.json",StringComparison.Ordinal)).Select(a=>a.Name+" / "+a.Representation+Environment.NewLine+File.ReadAllText(Path.Combine(result.Directory,a.Name))));
  timeline.Text=JsonSerializer.Serialize(result.Outcome.Events,Data.Json);
 }
 public void ShowComparison(string a,string b){report.Text=JsonSerializer.Serialize(BundleStore.Compare(a,b),Data.Json);status.Text="COMPARISON / requested leaf fields are counted; causal attribution and deterministic replay are not established.";}
 void ChooseOutput(){using var dialog=new FolderBrowserDialog();if(dialog.ShowDialog(this)==DialogResult.OK)outputFolder.Text=dialog.SelectedPath;}
 string? PickFolder(){using var dialog=new FolderBrowserDialog();return dialog.ShowDialog(this)==DialogResult.OK?dialog.SelectedPath:null;}
 void Open(){string? folder=PickFolder();if(folder is null)return;var v=BundleStore.Verify(folder);if(!v.Valid)throw new InvalidDataException(string.Join(Environment.NewLine,v.Issues));var outcome=BundleStore.ReadArtifact<Outcome>(folder,"outcome.json");var manifest=JsonSerializer.Deserialize<Manifest>(File.ReadAllText(Path.Combine(folder,"manifest.json")),Data.Json)!;ShowRun(new(folder,manifest.Id,v.Digest!,outcome));}
 void Compare(){if(currentDirectory is null)throw new InvalidOperationException("Open or run a bundle first.");string? other=PickFolder();if(other is not null)ShowComparison(currentDirectory,other);}
 void Guard(){if(currentDirectory is null)throw new InvalidOperationException("Open or run a bundle first.");report.Text=JsonSerializer.Serialize(BundleStore.ReplayGuard(currentDirectory,Data.Validate(Spec())),Data.Json);status.Text="GUARD REVIEW ONLY / current requested controls checked; no request sent. Unknown identities remain explicit; plugin replay blocked.";}
 void Export(){if(currentDirectory is null)throw new InvalidOperationException("Open or run a bundle first.");string? parent=PickFolder();if(parent is null)return;string destination=Path.Combine(parent,"experiment-"+Guid.NewGuid().ToString("N"));BundleStore.Export(currentDirectory,destination);status.Text="Exported exact verified bundle to a new directory. Preserve its digest separately for rewrite detection.";}
 public void SavePreview(string file){string real=outputFolder.Text;outputFolder.Text="Synthetic fixture output (local test directory)";try{using var image=new Bitmap(Width,Height);DrawToBitmap(image,new Rectangle(Point.Empty,Size));image.Save(file);}finally{outputFolder.Text=real;}}
}
