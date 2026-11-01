// SPDX-License-Identifier: MIT
using Experiments;using Experiments.Tests;using System.Text.Json;
namespace Experiments.App;
internal static class Program
{
 [STAThread] static void Main(string[] args)
 {
  ApplicationConfiguration.Initialize();
  if(args.Length==2&&args[0]=="--self-test"){
   string output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
   try{var tests=TestSuite.RunAsync(Path.Combine(output,"tests")).GetAwaiter().GetResult();
    using var form=new Workbench();form.Show();Application.DoEvents();
    var demo=CreateDemo(Path.Combine(output,"demo")).GetAwaiter().GetResult();form.ShowRun(demo.A);Application.DoEvents();form.SavePreview(Path.Combine(output,"workbench.png"));
    form.ShowComparison(demo.A.Directory,demo.B.Directory);Application.DoEvents();form.SavePreview(Path.Combine(output,"comparison.png"));
    File.WriteAllBytes(Path.Combine(output,"ui-result.json"),Data.Bytes(new{passed=tests.Failed==0,tests=tests.Passed,backend="controlled loopback stub; no LM Studio instance accessed",actualHttpClientUsed=true,realModelInference=false,credentialPersisted=false,baseline=demo.A.Id,variant=demo.B.Id}));form.Close();Environment.ExitCode=tests.Failed==0?0:1;
   }catch(Exception e){File.WriteAllText(Path.Combine(output,"self-test-error.txt"),e.ToString());Environment.ExitCode=1;}return;
  }
  Application.Run(new Workbench());
 }
 static async Task<(BundleResult A,BundleResult B)> CreateDemo(string folder){await using var stub=new StubServer();var spec=TestSuite.Spec(stub.Endpoint);var runner=new ExperimentRunner();var a=await runner.RunAsync(spec,folder);var b=await runner.RunAsync(spec with{Temperature=0.4},folder);return(a,b);}
}
