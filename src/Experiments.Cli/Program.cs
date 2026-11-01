// SPDX-License-Identifier: MIT
using Experiments;using Experiments.Tests;using System.Text.Json;
try{
 object result;int code=0;
 if(args.Length==2&&args[0]=="--self-test"){var tests=await TestSuite.RunAsync(args[1]);result=tests;code=tests.Failed==0?0:1;}
 else if(args.Length==2&&args[0]=="--stub-demo"){await using var stub=new StubServer();var runner=new ExperimentRunner();var s=TestSuite.Spec(stub.Endpoint);var a=await runner.RunAsync(s,args[1]);var b=await runner.RunAsync(s with{Temperature=0.4},args[1]);result=new{backend="controlled loopback stub; no inference",baseline=a,variant=b,comparison=BundleStore.Compare(a.Directory,b.Directory)};}
 else if(args.Length is 2 or 3&&args[0]=="--verify"){var check=BundleStore.Verify(args[1],args.Length==3?args[2]:null);result=check;code=check.Valid?0:2;}
 else if(args.Length==3&&args[0]=="--compare")result=BundleStore.Compare(args[1],args[2]);
 else if(args.Length==2&&args[0]=="--guard"){var s=BundleStore.ReadArtifact<ExperimentSpec>(args[1],"spec.json");result=BundleStore.ReplayGuard(args[1],s);}
 else if(args.Length==3&&args[0]=="--run"){
  var spec=JsonSerializer.Deserialize<ExperimentSpec>(File.ReadAllText(args[1]),Data.Json)??throw new ArgumentException("Null specification.");
  Console.Error.Write("Optional API token (not echoed or persisted; Enter for none): ");string token="";if(!Console.IsInputRedirected){ConsoleKeyInfo key;while((key=Console.ReadKey(true)).Key!=ConsoleKey.Enter){if(key.Key==ConsoleKey.Backspace&&token.Length>0)token=token[..^1];else if(!char.IsControl(key.KeyChar))token+=key.KeyChar;}Console.Error.WriteLine();}
  var run=await new ExperimentRunner().RunAsync(spec,args[2],token);result=run;code=run.Outcome.State=="completed"?0:2;
 }else{Console.WriteLine("LM Experiment Workbench\n --self-test NEW_OUTPUT\n --stub-demo NEW_PARENT\n --verify BUNDLE [INDEPENDENT_DIGEST]\n --compare BUNDLE BUNDLE\n --guard BUNDLE\n --run SPEC_JSON OUTPUT_PARENT\nRun explicitly contacts the selected loopback backend; models and plugins may execute. Tests use only an owned synthetic stub.");return args.Length==0?0:64;}
 Console.WriteLine(JsonSerializer.Serialize(result,Data.Json));return code;
}catch(Exception e){Console.Error.WriteLine(e.Message);return 1;}
