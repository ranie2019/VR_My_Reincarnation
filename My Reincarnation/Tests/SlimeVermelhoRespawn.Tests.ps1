$ErrorActionPreference='Stop'
$fixture=Get-Content -Raw (Join-Path $PSScriptRoot 'SlimeVerdeRespawn.Tests.ps1')
$injection=@'
$extra=@"
 public static int RunVermelho(){int n=0;Action<bool,string> c=(ok,msg)=>{if(!ok)throw new Exception(msg);n++;};
 if(RespawnMonstro.Instancia!=null)Call(RespawnMonstro.Instancia,"OnDisable");
 Physics.noGround=false;Physics.blocked=false;var m=new RespawnMonstro();var cfg=new RespawnMonstro.ConfiguracaoRespawnMonstro{idMonstro="Slime Vermelho",prefabRespawn=new GameObject(),tempoRespawn=15,offsetRespawn=Vector3.up};typeof(RespawnMonstro).GetField("configuracoesMonstros",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(m,new[]{cfg});Call(m,"Awake");Call(m,"OnEnable");var patrol=new[]{new Transform()};int previous=MonoBehaviour.born.Count;
 for(int cycle=0;cycle<3;cycle++){
 float start=Time.time;m.AgendarRespawnVermelho("Slime Vermelho",new Vector3(30,0,40),Quaternion.identity,patrol,100+cycle,.45f,.978f,new Vector3(0,.436f,0));m.AgendarRespawnVermelho("Slime Vermelho",new Vector3(30,0,40),Quaternion.identity,patrol,100+cycle,.45f,.978f,new Vector3(0,.436f,0));c(Pending(m)==1,"Red scheduling deduplicated per instance");var routine=m.routines[m.routines.Count-1];Time.time=start+14;routine.run.MoveNext();c(MonoBehaviour.born.Count==previous+cycle,"Red waits its own configured delay");Time.time=start+15;routine.run.MoveNext();var born=MonoBehaviour.born[previous+cycle];c(born.transform.position.x==30&&born.transform.position.y==1&&born.transform.position.z==40,"Red death position plus offset retained");c(born.slime.resets==1&&born.slime.patrol[0]==patrol[0],"Red fresh life and patrol reinitialized");c(Math.Abs(Physics.capsuleRadius-.4275f)<.001,"Red spawn checks its own larger capsule volume");}
 Physics.blocked=true;m.AgendarRespawnVermelho("Slime Vermelho",Vector3.zero,Quaternion.identity,patrol,200,.45f,.978f,new Vector3(0,.436f,0));Time.time+=15;m.routines[m.routines.Count-1].run.MoveNext();c(MonoBehaviour.born.Count==previous+3&&Pending(m)==1,"Blocked red volume waits without creating duplicates");return n;}
"@
$checks=$checks.Insert($checks.LastIndexOf('}'),$extra)
'@
$fixture=$fixture.Replace('Add-Type -TypeDefinition ($stub+$source+$checks)',$injection+"`nAdd-Type -TypeDefinition (`$stub+`$source+`$checks)")
$testDirectory=$PSScriptRoot
$fixture=$fixture.Replace('$PSScriptRoot','$testDirectory')
Invoke-Expression $fixture
Write-Output ("PASS: "+[RespawnChecks]::RunVermelho()+" verificacoes do respawn Vermelho real, com tres ciclos e volume proprio; host simulado.")
