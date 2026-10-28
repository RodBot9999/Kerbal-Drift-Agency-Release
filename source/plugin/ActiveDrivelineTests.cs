using System;
using KerbalDriftAgency;
class ActiveDrivelineTests {
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void Near(double a,double b,string message){Check(Math.Abs(a-b)<1e-9,message+" actual="+a+" expected="+b);}
 static DrivelineWheelState W(double weight=.25,double omega=100,double expected=100,double slip=0,bool grounded=true){return new DrivelineWheelState{Weight=weight,ShaftSpeed=omega,ExpectedShaftSpeed=expected,Slip=slip,Grounded=grounded};}
 static ActiveDrivelineConfig Config(){return new ActiveDrivelineConfig{Center=true,FrontVectoring=true,RearVectoring=true,CenterOffset=.95,CenterRate=1.5,SpeedThreshold=10,SpeedDifferenceThreshold=1,FrontOffset=.2,RearOffset=.3,FrontRate=1.5,RearRate=1.5,PassiveMode=1};}
 static ActiveDriveAllocation Run(ActiveDrivelineCore core,ActiveDrivelineConfig c,DrivelineWheelState fl,DrivelineWheelState fr,DrivelineWheelState rl,DrivelineWheelState rr,double desired=0,double yaw=0,bool powered=true,bool allowYaw=true,int steps=100){ActiveDriveAllocation a=new ActiveDriveAllocation();for(int i=0;i<steps;i++)a=core.Step(.02,c,fl,fr,rl,rr,desired,yaw,powered,allowYaw);return a;}
 static void Conserved(ActiveDriveAllocation a){Near(a.FL+a.FR+a.RL+a.RR,1,"Engine allocation conserved");Check(a.FL>=0&&a.FR>=0&&a.RL>=0&&a.RR>=0,"Nonnegative wheel fractions");}
 static void Main(){
  var c=Config();var core=new ActiveDrivelineCore();
  var a=Run(core,c,W(),W(),W(),W());Conserved(a);Near(a.RearShare,.5,"Symmetric steady AWD");
  a=Run(new ActiveDrivelineCore(),c,W(.2,160,160),W(.2,180,180),W(.3,90,90),W(.3,110,110));Near(a.RearShare,.6,"Normal corner/tire/gearing speed differences preserve 40:60");
  core.Reset();a=core.Step(.02,c,W(.25,200,100,.7),W(.25,200,100,.7),W(),W(),0,0,true,true);Near(a.RearShare,.53,"Source center rate limits first response");
  a=Run(core,c,W(.25,200,100,.7),W(.25,200,100,.7),W(),W());Check(a.RearShare>.7,"Front wheelspin sends drive rearward");Conserved(a);
  a=Run(core,c,W(),W(),W(.25,200,100,.7),W(.25,200,100,.7));Check(a.RearShare<.3,"Rear wheelspin sends drive forward");Conserved(a);
  c.CenterOffset=.1;a=Run(new ActiveDrivelineCore(),c,W(.25,10000,100,5),W(.25,10000,100,5),W(),W());Near(a.RearShare,.6,"Export center bias limit enforced");
  c=Config();c.Center=false;a=Run(new ActiveDrivelineCore(),c,W(.2,200,100,1),W(.2,200,100,1),W(.3),W(.3));Near(a.RearShare,.6,"Passive center keeps source axle split");
  c=Config();a=Run(new ActiveDrivelineCore(),c,W(.25,200,100,2),W(),W(),W());Near(a.FrontLeftShare,.3,"Front vector bias bound");Near(a.RearLeftShare,.5,"Independent rear state");Conserved(a);
  a=Run(new ActiveDrivelineCore(),c,W(),W(),W(),W(),.4,0);Check(a.FrontLeftShare>.5&&a.RearLeftShare>.5,"Right yaw demand gives more left wheel drive");
  var mirror=Run(new ActiveDrivelineCore(),c,W(),W(),W(),W(),-.4,0);Near(a.FL,mirror.FR,"Mirrored yaw response");Near(a.RL,mirror.RR,"Mirrored rear response");
  a=Run(new ActiveDrivelineCore(),c,W(),W(),W(),W(),1,0,true,false);Near(a.FrontLeftShare,.5,"Drift/airborne/reverse gate adds no yaw authority");
  c=Config();core=new ActiveDrivelineCore();a=Run(core,c,W(.25,300,100,2,false),W(.25,300,100,2,false),W(),W());Near(a.RearShare,.95,"Grounded axle gets available allocation within 5:95 safety bound");
  a=Run(core,c,W(),W(),W(),W(),1,0,false,true);Near(a.RearShare,.5,"Coasting returns center to baseline");Near(a.FrontLeftShare,.5,"No vectoring drive on throttle lift");
  a=Run(core,c,W(.25,1000,0,5,false),W(.25,500,0,3,false),W(.25,300,0,2,false),W(.25,100,0,1,false));Near(a.RearShare,.5,"Fully airborne center returns to baseline");Near(a.FrontLeftShare,.5,"Fully airborne axle avoids spin feedback");
  c=Config();c.Center=false;c.RearVectoring=false;a=Run(new ActiveDrivelineCore(),c,W(.5),W(.5),W(0),W(0));Near(a.RL+a.RR,0,"FWD receives no phantom rear drive");Conserved(a);
  c.FrontVectoring=false;c.RearVectoring=true;a=Run(new ActiveDrivelineCore(),c,W(0),W(0),W(.5),W(.5));Near(a.FL+a.FR,0,"RWD receives no phantom front drive");Conserved(a);
  a=Run(new ActiveDrivelineCore(),c,W(0),W(0),W(.5),W(.5),Double.NaN);Near(a.RearShare,1,"Invalid data fallback preserves original driven axle");Conserved(a);
  c=Config();c.Center=false;c.FrontVectoring=false;c.RearVectoring=false;
  a=Run(new ActiveDrivelineCore(),c,W(.2,200,100,1),W(.2),W(.3),W(.3));Near(a.FrontLeftShare,DrivelineCore.LeftShare(.2,.2,1,0,1),"Generic LSD retained on non-vectoring axle");Near(a.RearShare,.6,"No active center without source capability");
  var random=new Random(421);c=Config();core.Reset();
  for(int i=0;i<5000;i++){
   a=core.Step(.02,c,W(.2,random.NextDouble()*500,100,random.NextDouble()*2),W(.2,random.NextDouble()*500,100,random.NextDouble()*2),W(.3,random.NextDouble()*500,100,random.NextDouble()*2),W(.3,random.NextDouble()*500,100,random.NextDouble()*2),random.NextDouble()*2-1,random.NextDouble()*2-1,true,true);
   Conserved(a);Check(a.RearShare>=.05&&a.RearShare<=.95&&a.FrontLeftShare>=.3&&a.FrontLeftShare<=.7&&a.RearLeftShare>=.2&&a.RearLeftShare<=.8,"All imported bias limits maintained");
  }
  Console.WriteLine("PASS: active center/vectoring, source bounds/rates, geometry immunity, axle isolation, forward yaw signs, drift gate, coast/airborne reset, invalid input and 5000 conservation samples");
 }
}
