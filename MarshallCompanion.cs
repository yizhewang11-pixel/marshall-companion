using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ActonAuto {
[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class EnumeratorClass {}
[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface Enumerator {
 [PreserveSig] int EnumAudioEndpoints(int flow,uint mask,out Collection devices);
 [PreserveSig] int GetDefaultAudioEndpoint(int flow,int role,out Device device);
 [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id,out Device device);
}
[ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface Collection {
 [PreserveSig] int GetCount(out uint count); [PreserveSig] int Item(uint index,out Device device);
}
[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface Device {
 [PreserveSig] int Activate(ref Guid iid,uint context,IntPtr parameters,out IntPtr result);
 [PreserveSig] int OpenPropertyStore(uint mode,out PropertyStore store);
 [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
 [PreserveSig] int GetState(out uint state);
}
[StructLayout(LayoutKind.Sequential)] struct Key { public Guid format; public uint id; }
[StructLayout(LayoutKind.Explicit,Size=24)] struct Variant { [FieldOffset(0)] public ushort type; [FieldOffset(8)] public IntPtr pointer; }
[ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface PropertyStore {
 [PreserveSig] int GetCount(out uint count); [PreserveSig] int GetAt(uint index,out Key key);
 [PreserveSig] int GetValue(ref Key key,out Variant value);
}
[ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")] class PolicyClass {}
[ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface Policy {
 [PreserveSig] int GetMixFormat(string id,IntPtr p);
 [PreserveSig] int GetDeviceFormat(string id,int d,IntPtr p);
 [PreserveSig] int ResetDeviceFormat(string id);
 [PreserveSig] int SetDeviceFormat(string id,IntPtr a,IntPtr b);
 [PreserveSig] int GetProcessingPeriod(string id,int d,IntPtr a,IntPtr b);
 [PreserveSig] int SetProcessingPeriod(string id,IntPtr p);
 [PreserveSig] int GetShareMode(string id,IntPtr p);
 [PreserveSig] int SetShareMode(string id,IntPtr p);
 [PreserveSig] int GetPropertyValue(string id,ref Key k,out Variant v);
 [PreserveSig] int SetPropertyValue(string id,ref Key k,ref Variant v);
 [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id,int role);
}
// Read output levels only; never open a microphone or record audio.
[ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface PeakMeter {
 [PreserveSig] int GetPeakValue(out float peak);
}
class ActivitySnapshot {public bool Ready,Playing;public string Endpoint,Error;public DateTime Due;}
class OutputActivity {
 readonly object gate=new object();readonly CancellationTokenSource stop=new CancellationTokenSource();
 DateTime quietSince=DateTime.UtcNow,lastSample=DateTime.MinValue,lastSound=DateTime.MinValue;
 string endpoint="",error="正在检测音箱输出 / Detecting speaker output";bool ready;
 public OutputActivity(){Task.Factory.StartNew(Run,CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);}
 public void Stop(){stop.Cancel();}
 public void Reset(){lock(gate){quietSince=DateTime.UtcNow;}}
 public ActivitySnapshot Read(){lock(gate){var now=DateTime.UtcNow;return new ActivitySnapshot{Ready=ready&&(now-lastSample).TotalSeconds<2,Playing=ready&&(now-lastSound).TotalSeconds<2,Endpoint=endpoint,Error=error,Due=quietSince.AddMinutes(8).ToLocalTime()};}}
 static void Free(object value){if(value!=null&&Marshal.IsComObject(value))Marshal.ReleaseComObject(value);}
 void Run(){
  Enumerator en=null;Device device=null;PeakMeter meter=null;string current="";DateTime nextResolve=DateTime.MinValue;
  try{while(!stop.IsCancellationRequested){
   try{
    var now=DateTime.UtcNow;
    if(now>=nextResolve){
     nextResolve=now.AddSeconds(5);var devices=Audio.List().Where(x=>x.State==1).ToList();
     if(devices.Count!=1)throw new Exception("等待唯一的 所选音箱 输出 / Waiting for a single selected speaker output");
     if(current!=devices[0].Id||meter==null){
      Free(meter);meter=null;Free(device);device=null;Free(en);en=null;
      en=(Enumerator)new EnumeratorClass();Marshal.ThrowExceptionForHR(en.GetDevice(devices[0].Id,out device));
      Guid iid=typeof(PeakMeter).GUID;IntPtr raw=IntPtr.Zero;
      try{Marshal.ThrowExceptionForHR(device.Activate(ref iid,1,IntPtr.Zero,out raw));meter=(PeakMeter)Marshal.GetObjectForIUnknown(raw);}finally{if(raw!=IntPtr.Zero)Marshal.Release(raw);}
      current=devices[0].Id;lock(gate){endpoint=current;quietSince=now;lastSound=DateTime.MinValue;}
     }
    }
    uint state;Marshal.ThrowExceptionForHR(device.GetState(out state));if(state!=1)throw new Exception("音箱输出尚未就绪 / Speaker output is not ready");
    float peak;Marshal.ThrowExceptionForHR(meter.GetPeakValue(out peak));
    if(float.IsNaN(peak)||peak<0||peak>1)throw new Exception("音量检测返回无效读数 / Invalid audio meter reading");
    now=DateTime.UtcNow;
    lock(gate){
     // A sampling gap (including sleep/resume) is unknown, not proven silence.
     if(!ready||(now-lastSample).TotalSeconds>2||now<lastSample)quietSince=now;
     if(peak>0.0000001f){quietSince=now;lastSound=now;}
     lastSample=now;ready=true;error="";
    }
   }catch(Exception ex){
    lock(gate){ready=false;quietSince=DateTime.UtcNow;error=ex.Message;}
    Free(meter);meter=null;Free(device);device=null;Free(en);en=null;current="";nextResolve=DateTime.MinValue;
    if(stop.Token.WaitHandle.WaitOne(2000))break;
   }
   if(stop.Token.WaitHandle.WaitOne(50))break;
  }}finally{Free(meter);Free(device);Free(en);}
 }
}
class Endpoint { public string Id,Name; public uint State; }
static class Audio {
 [DllImport("ole32.dll")] static extern int PropVariantClear(ref Variant v);

 static void Free(object o) { if(o!=null && Marshal.IsComObject(o)) Marshal.ReleaseComObject(o); }
 public static volatile string TargetId="";
 public static List<Endpoint> List(bool all=false) {
  if(!all&&string.IsNullOrEmpty(TargetId))return new List<Endpoint>();
  var result=new List<Endpoint>(); var en=(Enumerator)new EnumeratorClass(); Collection col=null;
  try { Marshal.ThrowExceptionForHR(en.EnumAudioEndpoints(0,15,out col)); uint n; Marshal.ThrowExceptionForHR(col.GetCount(out n));
   for(uint i=0;i<n;i++) { Device d=null; PropertyStore s=null;
    try { Marshal.ThrowExceptionForHR(col.Item(i,out d)); string id; uint state; Marshal.ThrowExceptionForHR(d.GetId(out id)); Marshal.ThrowExceptionForHR(d.GetState(out state)); Marshal.ThrowExceptionForHR(d.OpenPropertyStore(0,out s));
     var key=new Key {format=new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),id=14}; Variant v;
     Marshal.ThrowExceptionForHR(s.GetValue(ref key,out v)); string name="";
     try { if(v.type==31) name=Marshal.PtrToStringUni(v.pointer); } finally { PropVariantClear(ref v); }
     if((all||id==TargetId) && !Regex.IsMatch(name??"","Hands.Free|AG Audio|免提|免持",RegexOptions.IgnoreCase) && BluetoothDirect.IsBluetooth(d)) result.Add(new Endpoint{Id=id,Name=name,State=state});
    } finally { Free(s); Free(d); }
   }
  } finally { Free(col); Free(en); } return result;
 }
 public static string Default(int role) {
  var en=(Enumerator)new EnumeratorClass(); Device d=null;
  try { if(en.GetDefaultAudioEndpoint(0,role,out d)!=0) return ""; string id; Marshal.ThrowExceptionForHR(d.GetId(out id)); return id; } finally { Free(d); Free(en); }
 }
 public static void Select(string id,Action checkAllowed) {
  var p=(Policy)new PolicyClass(); try { for(int role=0;role<2;role++){checkAllowed();Marshal.ThrowExceptionForHR(p.SetDefaultEndpoint(id,role));} } finally { Free(p); }
  if(Default(0)!=id || Default(1)!=id) throw new Exception("默认声音输出尚未切换成功 / Default audio output has not switched");
 }
}
// DeviceTopology + IKsControl route documented by Microsoft Core Audio.
[ComImport, Guid("2A07407E-6497-4A18-9787-32F79BD0D98F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface DeviceTopology {
 [PreserveSig] int GetConnectorCount(out uint count);
 [PreserveSig] int GetConnector(uint index,out Connector connector);
}
[ComImport, Guid("9C2C4058-23F5-41DE-877A-DF3AF236A09E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface Connector {
 [PreserveSig] int GetConnectorType(out int type);
 [PreserveSig] int GetDataFlow(out int flow);
 [PreserveSig] int ConnectTo(Connector connector);
 [PreserveSig] int Disconnect();
 [PreserveSig] int IsConnected([MarshalAs(UnmanagedType.Bool)] out bool connected);
 [PreserveSig] int GetConnectedTo(out Connector connector);
 [PreserveSig] int GetConnectorIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string id);
 [PreserveSig] int GetDeviceIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string id);
}
[StructLayout(LayoutKind.Sequential)] struct KsProperty {public Guid Set; public uint Id,Flags;}
[ComImport, Guid("28F54685-06FD-11D2-B27A-00A0C9223196"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface KsControl {
 [PreserveSig] int KsProperty(ref KsProperty property,uint propertyLength,IntPtr data,uint dataLength,out uint bytesReturned);
}
static class BluetoothDirect {
 static void Free(object o) {if(o!=null && Marshal.IsComObject(o))Marshal.ReleaseComObject(o);}
 static void Check(int hr,string phase) {if(hr<0)throw new Exception(phase+"失败，HRESULT=0x /  failed, HRESULT=0x"+hr.ToString("X8")+"："+Marshal.GetExceptionForHR(hr).Message);}
 static T Activate<T>(Device device,string phase) where T:class {
  Guid iid=typeof(T).GUID; IntPtr raw=IntPtr.Zero;
  try {
   Check(device.Activate(ref iid,1,IntPtr.Zero,out raw),phase);
   if(raw==IntPtr.Zero)throw new Exception(phase+"返回空接口 /  returned a null interface");
   return (T)Marshal.GetObjectForIUnknown(raw);
  } finally {if(raw!=IntPtr.Zero)Marshal.Release(raw);}
 }
 [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]static extern uint CM_Locate_DevNodeW(out uint node,string id,uint flags);
 [DllImport("cfgmgr32.dll")]static extern uint CM_Get_Parent(out uint parent,uint node,uint flags);
 [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]static extern uint CM_Get_Device_IDW(uint node,System.Text.StringBuilder id,uint length,uint flags);
 static bool BluetoothNode(string id){
  for(int i=0;i<12&&!string.IsNullOrEmpty(id);i++){
   if(Regex.IsMatch(id,@"^(BTHENUM|BTHHFENUM|BTHLEDEVICE|BTHA2DP|BTHLE)\\",RegexOptions.IgnoreCase))return true;
   uint node,parent;if(CM_Locate_DevNodeW(out node,id,1)!=0||CM_Get_Parent(out parent,node,0)!=0)return false;
   var b=new System.Text.StringBuilder(1024);if(CM_Get_Device_IDW(parent,b,1024,0)!=0)return false;id=b.ToString();
  }return false;
 }
 static bool BluetoothProperties(Device device){PropertyStore store=null;try{
  if(device.OpenPropertyStore(0,out store)<0)return false;
  var key=new Key{format=new Guid("78C34FC8-104A-4ACA-9EA4-524D52996E57"),id=256};Variant v;
  if(store.GetValue(ref key,out v)<0)return false;try{return v.type==31&&BluetoothNode(Marshal.PtrToStringUni(v.pointer));}finally{ClearVariant(ref v);}
 }finally{Free(store);}}
 [DllImport("ole32.dll",EntryPoint="PropVariantClear")]static extern int ClearVariant(ref Variant value);
 public static bool IsBluetooth(Device endpoint){
  Enumerator en=null;DeviceTopology topology=null;
  try{if(BluetoothProperties(endpoint))return true;en=(Enumerator)new EnumeratorClass();topology=Activate<DeviceTopology>(endpoint,"Topology");uint count;if(topology.GetConnectorCount(out count)<0||count>16)return false;
   for(uint i=0;i<count;i++){Connector connector=null;Device filter=null;try{
    if(topology.GetConnector(i,out connector)<0)continue;string id;if(connector.GetDeviceIdConnectedTo(out id)<0)continue;
    if(en.GetDevice(id,out filter)>=0&&BluetoothProperties(filter))return true;
   }finally{Free(filter);Free(connector);}}
  }catch(Exception){}finally{Free(topology);Free(en);}return false;
 }
 public static string TryConnect(string endpointId,CancellationToken token,Action checkAllowed) {
  Enumerator en=null; Device endpoint=null,filter=null; DeviceTopology topology=null; Connector connector=null; KsControl control=null;
  try {
   token.ThrowIfCancellationRequested();
   en=(Enumerator)new EnumeratorClass();
   Check(en.GetDevice(endpointId,out endpoint),"获取 所选音箱 音频设备 / Get selected speaker audio device");
   if(!IsBluetooth(endpoint))throw new Exception("无法确认所选设备为蓝牙音频设备 / Selected endpoint is not confirmed as Bluetooth audio");
   uint state; Check(endpoint.GetState(out state),"检查设备状态 / Check device state");
   if(state==1)return "所选音箱 已就绪，下一次检查将切换声音输出。 / selected speaker is ready; output will switch on the next check.";
   if((state&2)!=0)return "所选音箱 音频设备已被禁用；请先在 Windows 声音设置中启用。 / selected speaker audio device disabled; enable it in Windows sound settings.";
   topology=Activate<DeviceTopology>(endpoint,"打开设备拓扑 / Open device topology");
   uint count;Check(topology.GetConnectorCount(out count),"读取音频连接器 / Read audio connector");
   if(count!=1)return "该音频端点包含  / Audio endpoint connector count: "+count+" 个连接器，未发送请求，避免选错驱动。 /  connectors; request skipped to avoid the wrong driver.";
   Check(topology.GetConnector(0,out connector),"获取音频连接器 / Get audio connector");
   string filterId; Check(connector.GetDeviceIdConnectedTo(out filterId),"定位音箱的音频驱动 / Locate speaker audio driver");
   if(string.IsNullOrWhiteSpace(filterId))return "没有找到 所选音箱 的底层音频驱动。 / selected speaker audio driver not found.";
   Check(en.GetDevice(filterId,out filter),"获取音频驱动设备 / Get audio driver device");
   control=Activate<KsControl>(filter,"打开 IKsControl 后台连接接口 / Open IKsControl reconnect interface");
   // ksmedia.h: KSPROPSETID_BtAudio; RECONNECT=0, GET=1, NULL data.
   var request=new KsProperty{Set=new Guid("7FA06C40-B8F6-4C7E-8556-E8C33A12E54D"),Id=0,Flags=1};
   uint returned;token.ThrowIfCancellationRequested();checkAllowed();
   Check(control.KsProperty(ref request,(uint)Marshal.SizeOf(typeof(KsProperty)),IntPtr.Zero,0,out returned),"发送后台重连请求 / Send background reconnect request");
   return "已向 所选音箱 音频驱动发送重连请求；正在等待实际连接结果。 / Reconnect requested from selected speaker audio driver; waiting for actual connection.";
  } finally {Free(control);Free(filter);Free(connector);Free(topology);Free(endpoint);Free(en);}
 }
}
static class KeepAlive {
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Caps {public ushort Mid,Pid;public uint Version;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Name;public uint Formats;public ushort Channels,Reserved;public uint Support;}
 [StructLayout(LayoutKind.Sequential,Pack=2)] struct Format {public ushort Tag,Channels;public uint Rate,BytesPerSecond;public ushort BlockAlign,Bits,Extra;}
 [StructLayout(LayoutKind.Sequential)] struct Header {public IntPtr Data;public uint Length,Recorded;public UIntPtr User;public uint Flags,Loops;public IntPtr Next;public UIntPtr Reserved;}
 [DllImport("winmm.dll")]static extern uint waveOutGetNumDevs();
 [DllImport("winmm.dll",CharSet=CharSet.Unicode)]static extern uint waveOutGetDevCapsW(UIntPtr id,out Caps caps,uint size);
 [DllImport("winmm.dll")]static extern uint waveOutOpen(out IntPtr handle,uint id,ref Format format,IntPtr callback,IntPtr instance,uint flags);
 [DllImport("winmm.dll")]static extern uint waveOutPrepareHeader(IntPtr handle,IntPtr header,uint size);
 [DllImport("winmm.dll")]static extern uint waveOutWrite(IntPtr handle,IntPtr header,uint size);
 [DllImport("winmm.dll")]static extern uint waveOutUnprepareHeader(IntPtr handle,IntPtr header,uint size);
 [DllImport("winmm.dll")]static extern uint waveOutReset(IntPtr handle);
 [DllImport("winmm.dll")]static extern uint waveOutClose(IntPtr handle);
 static void Check(uint code,string step){if(code!=0)throw new Exception(step+"失败，音频错误码  /  failed, audio error code "+code);}
 [DllImport("winmm.dll")]static extern uint waveOutMessage(IntPtr device,uint message,IntPtr parameter1,IntPtr parameter2);
 static string WaveEndpoint(uint id){IntPtr size=Marshal.AllocHGlobal(IntPtr.Size),buffer=IntPtr.Zero;try{
  Marshal.WriteIntPtr(size,IntPtr.Zero);if(waveOutMessage(new IntPtr((long)id),0x812,size,IntPtr.Zero)!=0)return "";
  long bytes=Marshal.ReadIntPtr(size).ToInt64();if(bytes<2||bytes>65536||bytes%2!=0)return "";
  buffer=Marshal.AllocHGlobal((int)bytes);for(int i=0;i<bytes;i+=2)Marshal.WriteInt16(buffer,i,0);
  if(waveOutMessage(new IntPtr((long)id),0x811,buffer,new IntPtr(bytes))!=0)return "";
  return Marshal.PtrToStringUni(buffer)??"";
 }finally{if(buffer!=IntPtr.Zero)Marshal.FreeHGlobal(buffer);Marshal.FreeHGlobal(size);}}
 public static void Play(CancellationToken token,Action checkAllowed) {
  var active=Audio.List().Where(x=>x.State==1).ToList();
  if(active.Count!=1)throw new Exception("所选音箱输出未就绪 / Selected speaker output is not ready");
  string endpointId=active[0].Id;var ids=new List<uint>();
  for(uint i=0;i<waveOutGetNumDevs();i++)if(string.Equals(WaveEndpoint(i),endpointId,StringComparison.OrdinalIgnoreCase))ids.Add(i);
  if(ids.Count!=1)throw new Exception("无法唯一映射所选音箱的播放通道，保活已暂停 / Cannot uniquely map the selected speaker output; keep-awake paused");
  // PCM amplitude -50 dBFS. Smooth fades avoid clicks; no master-volume change.
  const int rate=44100,frames=88200;var bytes=new byte[frames*2];
  for(int i=0;i<frames;i++){double t=(double)i/rate;double fade=Math.Min(1,Math.Min(t/.15,(2-t)/.15));short sample=(short)Math.Round(32767*Math.Pow(10,-50.0/20)*fade*Math.Sin(2*Math.PI*220*t));bytes[i*2]=(byte)(sample&255);bytes[i*2+1]=(byte)((sample>>8)&255);}
  var format=new Format{Tag=1,Channels=1,Rate=rate,BytesPerSecond=rate*2,BlockAlign=2,Bits=16,Extra=0};
  IntPtr handle=IntPtr.Zero,data=IntPtr.Zero,header=IntPtr.Zero;bool prepared=false;uint size=(uint)Marshal.SizeOf(typeof(Header));
  try {
   token.ThrowIfCancellationRequested();checkAllowed();
   if(!string.Equals(WaveEndpoint(ids[0]),endpointId,StringComparison.OrdinalIgnoreCase)||!Audio.List().Any(x=>x.Id==endpointId&&x.State==1))throw new Exception("所选输出发生变化，保活已取消 / Selected output changed; keep-awake cancelled");
   Check(waveOutOpen(out handle,ids[0],ref format,IntPtr.Zero,IntPtr.Zero,0),"打开 音箱保活输出 / Open speaker keep-awake output");
   data=Marshal.AllocHGlobal(bytes.Length);Marshal.Copy(bytes,0,data,bytes.Length);
   header=Marshal.AllocHGlobal((int)size);Marshal.StructureToPtr(new Header{Data=data,Length=(uint)bytes.Length},header,false);
   Check(waveOutPrepareHeader(handle,header,size),"准备保活音 / Prepare keep-awake tone");prepared=true;
   checkAllowed();Check(waveOutWrite(handle,header,size),"发送保活音 / Send keep-awake tone");
   var watch=System.Diagnostics.Stopwatch.StartNew();
   while((((Header)Marshal.PtrToStructure(header,typeof(Header))).Flags&1)==0){token.ThrowIfCancellationRequested();checkAllowed();if(watch.ElapsedMilliseconds>5000)throw new Exception("保活播放超时。 / Keep-awake playback timed out.");Thread.Sleep(40);}
  } finally {
   bool safe=true;
   if(handle!=IntPtr.Zero){waveOutReset(handle);if(prepared)safe=waveOutUnprepareHeader(handle,header,size)==0;waveOutClose(handle);}
   // Never free a buffer still owned by a misbehaving driver. OS reclaims it at exit.
   if(safe){if(header!=IntPtr.Zero)Marshal.FreeHGlobal(header);if(data!=IntPtr.Zero)Marshal.FreeHGlobal(data);}
  }
 }
}
class QuietSchedule {
 public bool Enabled=true;public int Start=0,End=480;
 public bool Contains(DateTime now){if(!Enabled)return false;int minute=now.Hour*60+now.Minute;return Start==End|| (Start<End?minute>=Start&&minute<End:minute>=Start||minute<End);}
 public string Range {get{return Format(Start)+" – "+Format(End);}}
 public static string Format(int minutes){return (minutes/60).ToString("00")+":"+(minutes%60).ToString("00");}
}
class Reading {public string Title,Detail,Log;public bool Connected;public DateTime NextTone;public bool Playing;}
class Monitor {
 public readonly OutputActivity Activity=new OutputActivity();
 public volatile int RetryMinutes=1;
 public volatile QuietSchedule Schedule=new QuietSchedule();
 bool wasQuiet;int quietTransition;
 public void QuietTransition(){Interlocked.Exchange(ref quietTransition,1);}
 void Guard(CancellationToken token){token.ThrowIfCancellationRequested();if(Schedule.Contains(DateTime.Now))throw new OperationCanceledException();}
 bool selected=false,wasKeep=false,pendingSelect=false,keepFailed=false;DateTime nextAttempt=DateTime.MinValue,nextTone=DateTime.MinValue;
 public Reading Step(bool auto,bool keep,bool force,CancellationToken token){
  if(Interlocked.Exchange(ref quietTransition,0)!=0)wasQuiet=true;
  if(Schedule.Contains(DateTime.Now)){wasQuiet=true;wasKeep=false;pendingSelect=false;return new Reading{Title="夜间免打扰 / Quiet hours",Detail="自动重连与保活音已暂停 / Reconnect and keep-awake paused"};}
  if(wasQuiet){wasQuiet=false;nextAttempt=DateTime.MinValue;selected=false;Activity.Reset();}
  if(force){nextAttempt=DateTime.MinValue;selected=false;pendingSelect=true;}
  if(keep&&!wasKeep)Activity.Reset();wasKeep=keep;
  var r=new Reading{Title="等待连接 / Waiting to connect",Detail="正在检查 所选音箱 / Checking selected speaker",NextTone=nextTone};
  try {
   token.ThrowIfCancellationRequested();var devices=Audio.List();var active=devices.Where(x=>x.State==1).ToList();
   if(active.Count>1){r.Title="需要确认设备 / Device confirmation needed";r.Detail="检测到多个同名输出，请查看日志 / Multiple matching outputs; see log";r.Log="检测到多个 所选音箱 活动端点，未自动选择。 / Multiple active selected speaker endpoints; no output selected.";return r;}
   if(active.Count==1){
    r.Connected=true;r.Title="已连接 / Connected";r.Detail=keep?(keepFailed?"保活未成功 · 查看日志 / Keep-awake failed; see log":"保持唤醒已开启 / Keep awake enabled"):"保持唤醒已关闭 / Keep awake disabled";
    if((auto||force||pendingSelect)&&!selected){token.ThrowIfCancellationRequested();try{Guard(token);Audio.Select(active[0].Id,()=>Guard(token));r.Log="已连接，并将系统声音输出切换到 所选音箱。 / Connected; system audio switched to selected speaker.";}catch(OperationCanceledException){throw;}catch(Exception ex){r.Log="音箱已连接，但自动切换输出失败： / Connected, but changing the audio output failed: "+ex.Message;}selected=true;pendingSelect=false;}
    if(keep){
     var activity=Activity.Read();
     if(!activity.Ready||activity.Endpoint!=active[0].Id){r.Detail="音量检测未就绪 · 暂停保活 / Audio meter unavailable; keep-awake paused";r.Log=activity.Error;r.NextTone=DateTime.MinValue;return r;}
     nextTone=activity.Due;r.Playing=activity.Playing;
     r.Detail=activity.Playing?"正在播放 · 无需保活 / Playing audio; no keep-awake needed":(keepFailed?"保活未成功 · 查看日志 / Keep-awake failed; see log":"静音计时中 · 按需保持唤醒 / Timing silence; keep awake as needed");
     if(!activity.Playing&&DateTime.Now>=nextTone){
      token.ThrowIfCancellationRequested();activity=Activity.Read();
      if(activity.Ready&&activity.Endpoint==active[0].Id&&!activity.Playing&&DateTime.Now>=activity.Due){
       Activity.Reset();
       try{Guard(token);KeepAlive.Play(token,()=>Guard(token));keepFailed=false;r.Detail="已完成保活 · 重新等待无声 / Keep-awake completed; restarting silence timer";r.Log="连续约 8 分钟未检测到声音，已播放 2 秒保活音。 / No sound for about 8 minutes; played a 2-second keep-awake tone.";}
       catch(OperationCanceledException){throw;}
       catch(Exception ex){keepFailed=true;r.Detail="保活未成功 · 查看日志 / Keep-awake failed; see log";r.Log=ex.Message;}
       finally{Activity.Reset();}
       nextTone=Activity.Read().Due;
      }
     }
    }
    r.NextTone=nextTone;return r;
   }
   selected=false;
   if(devices.Count==0){r.Detail="请先配对并在检测与设置中选择音箱 / Pair and select a speaker in Checks & setup first";return r;}
   if(devices.All(x=>x.State==2)){r.Detail="音频设备已被禁用，请在系统中启用 / Audio device disabled; enable it in Windows";return r;}
   if(!auto&&!force){r.Detail="自动重连已暂停 / Auto reconnect paused";return r;}
   if(devices.Count!=1){r.Detail="存在多个同名端点，未自动连接 / Multiple matching endpoints; reconnect skipped";return r;}
   r.Detail="将自动尝试后台连接 / Will attempt reconnection in the background";
   if(DateTime.Now<nextAttempt)return r;
   nextAttempt=DateTime.Now.AddMinutes(RetryMinutes);r.Title="正在连接 / Connecting";Guard(token);r.Log=BluetoothDirect.TryConnect(devices[0].Id,token,()=>Guard(token));r.Detail="已请求连接，等待音箱响应 / Connection requested; waiting for speaker";return r;
  }catch(OperationCanceledException){r.Title="已暂停 / Paused";r.Detail="等待下一次检查 / Waiting for next check";return r;}
   catch(Exception ex){r.Title=r.Connected?"已连接 / Connected":"暂未连接 / Disconnected";r.Detail="操作未完成 · 查看日志 / Operation incomplete; see log";r.Log=ex.Message;nextAttempt=DateTime.Now.AddMinutes(RetryMinutes);return r;}
 }
}
class Settings {
 public string TargetId="";
 public bool Auto=true,Keep=true,Night=true;public int Start=0,End=480,Retry=1;
 public static string Folder=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MarshallCompanion");
 public static string FileName=System.IO.Path.Combine(Folder,"settings.json");
 public void Validate(){if(Start<0||Start>1439||End<0||End>1439||Retry<1||Retry>60)throw new Exception("设置无效 / Invalid settings");}
 public void Save(){Validate();System.IO.Directory.CreateDirectory(Folder);string temp=FileName+".tmp";System.IO.File.WriteAllText(temp,new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(this));if(System.IO.File.Exists(FileName))System.IO.File.Replace(temp,FileName,null);else System.IO.File.Move(temp,FileName);}
 public static Settings Load(){if(!System.IO.File.Exists(FileName))return new Settings();var v=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Settings>(System.IO.File.ReadAllText(FileName));if(v==null)throw new Exception("Empty settings");v.Validate();return v;}
}
class Window:Form {
 [DllImport("user32.dll")]static extern bool ReleaseCapture();
 [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,int m,IntPtr w,IntPtr l);
 [DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr h);
 readonly Monitor monitor=new Monitor();readonly System.Web.Script.Serialization.JavaScriptSerializer json=new System.Web.Script.Serialization.JavaScriptSerializer();
 Microsoft.Web.WebView2.WinForms.WebView2 web;Settings settings;NotifyIcon tray;Icon appIcon;System.Windows.Forms.Timer timer;
 CancellationTokenSource cancel=new CancellationTokenSource();Task<Reading> job;Reading reading=new Reading();DateTime next=DateTime.MinValue;
 bool startupBusy;string startupMessage="";
 bool diagnosing; string diagnostics="尚未自检 / Not checked",testResult="尚未测试 / Not tested"; List<Endpoint> candidates=new List<Endpoint>();
 int layoutHeight=1100; double preferredScale=0.8;
 bool force,exit,ready,lastQuiet;readonly bool startHidden;List<string> logs=new List<string>();string lastLog="";
 public Window(bool hidden){startHidden=hidden;Text="Marshall Companion";BackColor=Color.FromArgb(29,29,29);FormBorderStyle=FormBorderStyle.None;Padding=new Padding(4);StartPosition=FormStartPosition.CenterScreen;
  var area=Screen.PrimaryScreen.WorkingArea;ClientSize=new Size(Math.Min(504,area.Width-24),Math.Min(940,area.Height-40));
  try{settings=Settings.Load();}catch(Exception ex){settings=new Settings();Log("设置读取失败，使用默认值 / Could not load settings: "+ex.Message);}
  ApplySettings();appIcon=MakeIcon();Icon=appIcon;
  var menu=new ContextMenuStrip();menu.Items.Add("打开 / Open",null,(s,e)=>Restore());menu.Items.Add("退出 / Exit",null,(s,e)=>{exit=true;Close();});
  tray=new NotifyIcon{Icon=appIcon,Text="Marshall Companion",Visible=true,ContextMenuStrip=menu};tray.MouseClick+=(s,e)=>{if(e.Button==MouseButtons.Left)Restore();};
  web=new Microsoft.Web.WebView2.WinForms.WebView2{Dock=DockStyle.Fill,DefaultBackgroundColor=BackColor};Controls.Add(web);
  timer=new System.Windows.Forms.Timer{Interval=500};timer.Tick+=(s,e)=>Tick();timer.Start();
  Shown+=async(s,e)=>{if(startHidden)HideTray();try{
   var env=await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null,System.IO.Path.Combine(Settings.Folder,"WebView"));await web.EnsureCoreWebView2Async(env);
   var core=web.CoreWebView2;core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.AreDevToolsEnabled=false;core.Settings.IsStatusBarEnabled=false;
   core.SetVirtualHostNameToFolderMapping("marshall.local",AppDomain.CurrentDomain.BaseDirectory,Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.DenyCors);
   core.NavigationStarting+=(o,a)=>{if(a.Uri!="https://marshall.local/index.html")a.Cancel=true;};core.NewWindowRequested+=(o,a)=>a.Handled=true;
   core.PermissionRequested+=(o,a)=>a.State=Microsoft.Web.WebView2.Core.CoreWebView2PermissionState.Deny;
   core.WebMessageReceived+=(o,a)=>{if(a.Source=="https://marshall.local/index.html")Receive(a.WebMessageAsJson);};
   web.Source=new Uri("https://marshall.local/index.html");
  }catch(Exception ex){MessageBox.Show("界面启动失败。请安装 Microsoft Edge WebView2 Runtime 后重试。\nUI startup failed. Install Microsoft Edge WebView2 Runtime and try again.\nhttps://developer.microsoft.com/microsoft-edge/webview2/\n\n"+ex.Message,Text);exit=true;Close();}};
  FormClosing+=(s,e)=>{if(!exit&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;HideTray();}};
  Resize+=(s,e)=>{if(WindowState==FormWindowState.Minimized)HideTray();};
  FormClosed+=(s,e)=>{timer.Stop();cancel.Cancel();monitor.Activity.Stop();tray.Visible=false;tray.Dispose();menu.Dispose();appIcon.Dispose();timer.Dispose();};
 }
 [StructLayout(LayoutKind.Sequential)]struct ResizeRect {public int Left,Top,Right,Bottom;}
 protected override void WndProc(ref Message m){
  const int WM_NCHITTEST=0x84,WM_SIZING=0x214;
  if(m.Msg==WM_NCHITTEST){
   base.WndProc(ref m);long xy=m.LParam.ToInt64();var pt=PointToClient(new Point((short)(xy&65535),(short)((xy>>16)&65535)));
   bool l=pt.X<4,r=pt.X>=ClientSize.Width-4,t=pt.Y<4,b=pt.Y>=ClientSize.Height-4;
   int hit=t?(l?13:r?14:12):b?(l?16:r?17:15):l?10:r?11:0;
   if(hit!=0)m.Result=new IntPtr(hit);return;
  }
  if(m.Msg==WM_SIZING){
   var rect=(ResizeRect)Marshal.PtrToStructure(m.LParam,typeof(ResizeRect));int edge=m.WParam.ToInt32();
   var area=Screen.FromControl(this).WorkingArea;
   double scale=(edge==3||edge==6)?(rect.Bottom-rect.Top-8)/(double)layoutHeight:(rect.Right-rect.Left-8)/620.0;
   double maximum=Math.Min((area.Width-8)/620.0,(area.Height-8)/(double)layoutHeight);
   scale=Math.Min(maximum,Math.Max(0.4,scale));preferredScale=scale;
   int width=(int)Math.Round(620*scale)+8,height=(int)Math.Round(layoutHeight*scale)+8;
   if(edge==1||edge==4||edge==7)rect.Left=rect.Right-width;else rect.Right=rect.Left+width;
   if(edge==3||edge==4||edge==5)rect.Top=rect.Bottom-height;else rect.Bottom=rect.Top+height;
   Marshal.StructureToPtr(rect,m.LParam,false);m.Result=new IntPtr(1);return;
  }
  base.WndProc(ref m);
 }
 Icon MakeIcon(){using(var bmp=new Bitmap(32,32)){using(var g=Graphics.FromImage(bmp)){g.Clear(Color.Transparent);g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using(var b=new SolidBrush(Color.FromArgb(25,25,25)))g.FillEllipse(b,0,0,31,31);using(var p=new Pen(Color.FromArgb(212,179,111),1))g.DrawEllipse(p,1,1,29,29);string logo=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"emblem.png");if(System.IO.File.Exists(logo))using(var img=Image.FromFile(logo)){g.DrawImage(img,new Rectangle(1,0,30,30),new Rectangle(65,40,650,650),GraphicsUnit.Pixel);}else using(var f=new Font("Segoe Script",18,FontStyle.Bold))using(var b=new SolidBrush(Color.Goldenrod))g.DrawString("M",f,b,0,0);}var h=bmp.GetHicon();try{return (Icon)Icon.FromHandle(h).Clone();}finally{DestroyIcon(h);}}}
 void HideTray(){Hide();ShowInTaskbar=false;}
 void Restore(){ShowInTaskbar=true;Show();WindowState=FormWindowState.Normal;Activate();}
 void ApplySettings(){Audio.TargetId=settings.TargetId??"";monitor.Schedule=new QuietSchedule{Enabled=settings.Night,Start=settings.Start,End=settings.End};monitor.RetryMinutes=settings.Retry;monitor.QuietTransition();cancel.Cancel();next=DateTime.MinValue;}
 void Log(string value){if(string.IsNullOrWhiteSpace(value)||lastLog==value)return;lastLog=value;logs.Insert(0,DateTime.Now.ToString("HH:mm:ss")+"  "+value);if(logs.Count>80)logs.RemoveAt(logs.Count-1);}
 int Number(Dictionary<string,object> d,string key){return Convert.ToInt32(d[key]);}
 void Receive(string value){try{var d=json.Deserialize<Dictionary<string,object>>(value);string action=Convert.ToString(d["action"]);
  if(action=="ready"){ready=true;Push();return;}
  if(action=="layout"){int h=Number(d,"height");if(h<400||h>2400)return;layoutHeight=h;var a=Screen.FromControl(this).WorkingArea;double scale=Math.Min(preferredScale,Math.Min((a.Width-32)/620.0,(a.Height-48)/(double)h));ClientSize=new Size((int)Math.Ceiling(620*scale)+8,(int)Math.Ceiling(h*scale)+8);Left=Math.Max(a.Left,Math.Min(Left,a.Right-Width));Top=Math.Max(a.Top,Math.Min(Top,a.Bottom-Height));return;}
  if(action=="drag"){ReleaseCapture();SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);return;}
  if(action=="tray"){HideTray();return;}
  if(action=="connect"){if(!diagnosing&&!monitor.Schedule.Contains(DateTime.Now)&&!reading.Connected){force=true;next=DateTime.MinValue;}return;}
  if(action=="startup"){SetStartup(Convert.ToBoolean(d["enabled"]));return;}
  if(action=="inspect"){Diagnose(false);return;}
  if(action=="test-reconnect"){Diagnose(true);return;}
  if(action=="test-tone"){Diagnose(false,true);return;}
  if(action=="select-device"){
   if(diagnosing){Log("测试进行中，请稍候 / Test running; please wait");return;}
   string id=Convert.ToString(d["id"]);if(id!=""&&!candidates.Any(x=>x.Id==id))throw new Exception("请先刷新设备列表 / Refresh the device list first");
   string old=settings.TargetId;settings.TargetId=id;try{settings.Save();}catch{settings.TargetId=old;throw;}
   ApplySettings();reading=new Reading();monitor.Activity.Reset();testResult="设备已更改，尚未测试 / Device changed; not tested";Log("目标设备已保存 / Target device saved");Push();return;
  }
  if(action=="settings"){
   if(diagnosing)throw new Exception("请等待测试结束后修改设置 / Wait until the test finishes before changing settings");
   var candidate=new Settings{TargetId=settings.TargetId,Auto=Convert.ToBoolean(d["auto"]),Keep=Convert.ToBoolean(d["keep"]),Night=Convert.ToBoolean(d["night"]),Start=Number(d,"start"),End=Number(d,"end"),Retry=Number(d,"retry")};candidate.Save();settings=candidate;ApplySettings();Log("设置已保存 / Settings saved");Push();
  }
 }catch(Exception ex){Log("操作失败 / Action failed: "+ex.Message);Push();}}
 bool StartupEnabled(){return System.IO.File.Exists(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"Marshall Companion.lnk"));}
 async void SetStartup(bool enabled){
  if(startupBusy)return;startupBusy=true;startupMessage="正在设置 / Applying…";Push();
  try{
   string script=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Autostart.ps1");
   string powershell=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),@"System32\WindowsPowerShell\v1.0\powershell.exe");
   string error=await Task.Run(()=>{
    var info=new System.Diagnostics.ProcessStartInfo(powershell,"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \""+script+"\" -Quiet"+(enabled?"":" -Disable")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};
    using(var process=System.Diagnostics.Process.Start(info)){string stderr=process.StandardError.ReadToEnd();process.WaitForExit();if(process.ExitCode!=0)return string.IsNullOrWhiteSpace(stderr)?"自启动脚本执行失败 / Startup setup failed":stderr;return "";}
   });
   if(error!="")throw new Exception(error);
   if(StartupEnabled()!=enabled)throw new Exception("自启动状态未更新 / Startup state did not update");
   startupMessage=enabled?"已开启：下次登录在托盘运行 / Enabled: starts in tray at next sign-in":"已关闭开机自启动 / Login startup disabled";Log(startupMessage);
  }catch(Exception ex){startupMessage="设置失败 / Setup failed: "+ex.Message;Log(startupMessage);}
  finally{startupBusy=false;if(!IsDisposed&&!exit)Push();}
 }
 async void Diagnose(bool reconnect,bool tone=false){
  if(diagnosing)return;diagnosing=true;cancel.Cancel();force=false;
  diagnostics="正在检查 / Checking…";if(reconnect||tone)testResult="正在准备测试 / Preparing test…";Push();
  try{
   if(job!=null){try{await job;}catch(OperationCanceledException){}catch(Exception ex){Log(ex.Message);}job=null;}
   cancel.Dispose();cancel=new CancellationTokenSource();var token=cancel.Token;
   candidates=await Task.Run(()=>Audio.List(true));
   var target=candidates.Where(x=>!string.IsNullOrEmpty(settings.TargetId)&&x.Id==settings.TargetId).ToList();
   diagnostics="环境已就绪：Windows / x64 / .NET / WebView2 " + web.CoreWebView2.Environment.BrowserVersionString +
    "\nEnvironment ready: Windows / x64 / .NET / WebView2"+
    "\n可识别设备 / Matching devices: "+candidates.Count+
    "\n"+(target.Count==1?"目标设备 / Target: "+target[0].Name:target.Count==0?"未找到目标，请先配对或重新选择 / Target missing; pair or select again":"多个候选，请选择目标设备 / Multiple candidates; select a target")+
    "\n运行环境就绪不代表重连驱动兼容；请执行重连测试。 / Environment readiness does not prove driver compatibility; run the reconnect test.";
   if(tone){
    if(monitor.Schedule.Contains(DateTime.Now))testResult="免打扰中，未播放测试音 / Quiet hours; no test tone played";
    else if(target.Count!=1||target[0].State!=1)testResult="请先选择并连接音箱 / Select and connect a speaker first";
    else{testResult="正在播放轻声测试音 / Playing a quiet test tone…";Push();await Task.Run(()=>KeepAlive.Play(token,()=>{token.ThrowIfCancellationRequested();if(monitor.Schedule.Contains(DateTime.Now))throw new OperationCanceledException();}));monitor.Activity.Reset();testResult="已完成所选音箱的 2 秒测试音播放；能否长期防止休眠仍需观察。 / Completed 2-second playback to the selected output; long-term standby prevention remains unverified.";}
    Log(testResult);
   }
   if(reconnect){
    if(monitor.Schedule.Contains(DateTime.Now)){testResult="免打扰中，未发送请求 / Quiet hours; no request sent";}
    else if(target.Count!=1){testResult="请先选择唯一目标设备 / Select one target device first";}
    else if(target[0].State==1){testResult="设备已实际连接；未断开测试，重连能力仍未验证 / Already connected; no disconnect performed. Reconnect capability remains unverified.";reading=new Reading{Connected=true};}
    else if((target[0].State&2)!=0){testResult="设备已禁用，请先在 Windows 启用 / Device disabled; enable it in Windows first";}
    else{
     string id=target[0].Id;testResult="正在向驱动发送请求 / Sending driver request…";Push();
     testResult=await Task.Run(()=>BluetoothDirect.TryConnect(id,token,()=>{token.ThrowIfCancellationRequested();if(monitor.Schedule.Contains(DateTime.Now))throw new OperationCanceledException();}));
     Push();bool connected=false;
     for(int i=0;i<20;i++){
      if(monitor.Schedule.Contains(DateTime.Now))throw new OperationCanceledException();
      connected=await Task.Run(()=>Audio.List().Any(x=>x.Id==id&&x.State==1));if(connected)break;
      await Task.Delay(1000,token);
     }
     testResult=connected?"已实际连接：本次重连测试通过 / Actual connection detected: this reconnect test passed":"20 秒内未检测到连接，不能确认兼容。检查音箱电源、占用情况与驱动。 / No connection detected within 20 seconds; compatibility is unconfirmed. Check power, other connections and the driver.";
     if(connected)reading=new Reading{Connected=true};
    }
    Log(testResult);
   }
  }catch(OperationCanceledException){testResult="测试已暂停（免打扰或退出） / Test paused (quiet hours or exit)";}
   catch(Exception ex){diagnostics="检查或测试未完成 / Check or test incomplete: "+ex.Message;testResult="未通过或无法确认，请查看详情 / Failed or unconfirmed; see details";Log(diagnostics);}
   finally{diagnosing=false;next=DateTime.Now.AddSeconds(5);if(!IsDisposed&&!exit)Push();}
 }
 void Tick(){if(diagnosing){Push();return;}bool quiet=monitor.Schedule.Contains(DateTime.Now);if(quiet!=lastQuiet){lastQuiet=quiet;cancel.Cancel();monitor.QuietTransition();force=false;next=DateTime.MinValue;monitor.Activity.Reset();Log(quiet?"免打扰开始 / Quiet hours started":"免打扰结束 / Quiet hours ended");}
  if(job!=null&&job.IsCompleted){if(job.IsCanceled){}else if(job.IsFaulted)Log("后台操作失败 / Background operation failed: "+job.Exception.GetBaseException().Message);else{reading=job.Result;Log(reading.Log);}job=null;}
  if(job==null&&(DateTime.Now>=next||force)){
   if(cancel.IsCancellationRequested){cancel.Dispose();cancel=new CancellationTokenSource();}var token=cancel.Token;bool a=settings.Auto,k=settings.Keep,f=force;force=false;next=DateTime.Now.AddSeconds(5);
   if(quiet){job=Task.Run(()=>{token.ThrowIfCancellationRequested();var active=Audio.List().Where(x=>x.State==1).ToList();return new Reading{Connected=active.Count==1};});}
   else job=Task.Run(()=>monitor.Step(a,k,f,token));
  }
  Push();
 }
 void Push(){if(!ready||web.CoreWebView2==null)return;bool quiet=monitor.Schedule.Contains(DateTime.Now);
  tray.Text="Marshall Companion · "+(quiet?"Quiet hours":reading.Connected?"Connected":"Waiting");
  var payload=new {startup=StartupEnabled(),startupBusy=startupBusy,startupMessage=startupMessage,devices=candidates.Select(x=>new {id=x.Id,name=x.Name,state=x.State}).ToArray(),targetId=settings.TargetId??"",diagnostics=diagnostics,testResult=testResult,diagnosing=diagnosing,auto=settings.Auto,keep=settings.Keep,night=settings.Night,start=QuietSchedule.Format(settings.Start),end=QuietSchedule.Format(settings.End),retryMinutes=settings.Retry,connected=reading.Connected,playing=reading.Playing,quiet=quiet,busy=job!=null&&!reading.Connected&&!quiet,detail=reading.Detail??"",logs=logs.ToArray(),seconds=reading.NextTone>DateTime.Now?(int)(reading.NextTone-DateTime.Now).TotalSeconds:0};
  try{web.CoreWebView2.PostWebMessageAsJson(json.Serialize(payload));}catch(Exception ex){Log("界面同步失败 / UI sync failed: "+ex.Message);}
 }
 [STAThread]public static void Main(string[] args){bool created;using(var mutex=new Mutex(true,"Local\\ActonAutoHelperV2",out created)){bool hidden=Array.IndexOf(args,"--tray")>=0;if(!created){if(!hidden)MessageBox.Show("程序已在运行，请从托盘打开。\nAlready running. Open it from the system tray.","Marshall Companion");return;}try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new Window(hidden));}finally{mutex.ReleaseMutex();}}}
}
}
