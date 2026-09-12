// 在隔离 Editor 中使用真实 CommandWatcher 和拒绝删除共享的 FileStream 验证。
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using AIBridge.Editor;
using UnityEditor;

var root=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../.aibridge/lock-validation",Guid.NewGuid().ToString("N")));
var watcher=new CommandWatcher(root);
var checks=new List<object>();
string FileAt(string folder,string id)=>Path.Combine(root,folder,id+".json");
void Submit(string id,string body){
 File.WriteAllText(FileAt("commands",id),JsonConvert.SerializeObject(new {protocolVersion=2,id,type="CodeExecuteCommand_Execute",@params=new {code=body}}));
}
async Task<JObject> WaitResult(string id){
 var deadline=DateTime.UtcNow.AddSeconds(10);
 while(!File.Exists(FileAt("results",id))){
  if(DateTime.UtcNow>deadline)throw new Exception("Missing result: "+id);
  watcher.ScanForCommands();watcher.ProcessOneCommand();await Task.Delay(30);
 }
 return JObject.Parse(File.ReadAllText(FileAt("results",id)));
}
string CounterCode(string key)=>"UnityEditor.SessionState.SetInt("+JsonConvert.SerializeObject(key)+",UnityEditor.SessionState.GetInt("+JsonConvert.SerializeObject(key)+",0)+1); return true;";

var key1="AIBridge.Lock.Released."+Guid.NewGuid().ToString("N");
Submit("released",CounterCode(key1));watcher.ScanForCommands();
using(var held=new FileStream(FileAt("status","released"),FileMode.Open,FileAccess.Read,FileShare.Read)){
 if(watcher.ProcessOneCommand())throw new Exception("Locked command left queue");
 if(SessionState.GetInt(key1,0)!=0)throw new Exception("Executed before running status published");
}
watcher.ProcessOneCommand();var first=await WaitResult("released");
if(!(bool)first["success"]||SessionState.GetInt(key1,0)!=1)throw new Exception("Released command did not execute exactly once");
Submit("released",CounterCode(key1));watcher.ScanForCommands();watcher.ProcessOneCommand();
if(SessionState.GetInt(key1,0)!=1)throw new Exception("Duplicate execution");
checks.Add(new {name="lock_release_executes_once",passed=true});

var key2="AIBridge.Lock.Persistent."+Guid.NewGuid().ToString("N");
Submit("persistent",CounterCode(key2));watcher.ScanForCommands();
using(var held=new FileStream(FileAt("status","persistent"),FileMode.Open,FileAccess.Read,FileShare.Read)){
 if(watcher.ProcessOneCommand())throw new Exception("Persistent lock ignored");
 await Task.Delay(2200);
 watcher.ProcessOneCommand();var rejected=await WaitResult("persistent");
 if((string)rejected["errorCode"]!="STATUS_WRITE_FAILED"||SessionState.GetInt(key2,0)!=0)throw new Exception("Persistent failure lost or executed command");
}
watcher.ScanForCommands();
checks.Add(new {name="persistent_lock_reports_not_executed",passed=true});

var key3="AIBridge.Lock.Complete."+Guid.NewGuid().ToString("N");
var handleKey=key3+".handle";
Submit("completed",CounterCode(key3).Replace("return true;", "System.AppDomain.CurrentDomain.SetData("+JsonConvert.SerializeObject(handleKey)+",new System.IO.FileStream("+JsonConvert.SerializeObject(FileAt("status","completed"))+",System.IO.FileMode.Open,System.IO.FileAccess.Read,System.IO.FileShare.Read)); return true;"));
try{
 watcher.ScanForCommands();watcher.ProcessOneCommand();var completed=await WaitResult("completed");
 if(!(bool)completed["success"]||SessionState.GetInt(key3,0)!=1)throw new Exception("Status lock damaged completion");
 var original=File.ReadAllText(FileAt("results","completed"));
 ((IDisposable)AppDomain.CurrentDomain.GetData(handleKey)).Dispose();AppDomain.CurrentDomain.SetData(handleKey,null);
 watcher.ScanForCommands();watcher.ProcessOneCommand();
 if(File.ReadAllText(FileAt("results","completed"))!=original||SessionState.GetInt(key3,0)!=1)throw new Exception("Completed result changed");
 checks.Add(new {name="completion_lock_preserves_result",passed=true});
}finally{
 (AppDomain.CurrentDomain.GetData(handleKey) as IDisposable)?.Dispose();AppDomain.CurrentDomain.SetData(handleKey,null);
}
return new {passed=true,checks};
