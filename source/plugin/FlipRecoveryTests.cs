using System;
class FlipRecoveryTests {
 static int n;static void Check(bool result,string label){n++;if(!result)throw new Exception(label);}
 static void Main(){
  foreach(bool active in new[]{false,true})foreach(bool landed in new[]{false,true})foreach(bool packed in new[]{false,true})foreach(bool water in new[]{false,true})foreach(double up in new[]{-1.0,-.75,-.5,-.49,0,1})foreach(double speed in new[]{0,.49,.5,.5001,5})foreach(double spin in new[]{0,.2,.21}){
   bool expected=active&&landed&&!packed&&!water&&up<=-.5&&speed<=.5&&spin<=.2;
   Check(FlipRecoveryCore.Eligible(active,landed,packed,water,up,speed,spin)==expected,"Gate state combination");
  }
  foreach(double invalid in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity}){
   Check(!FlipRecoveryCore.Eligible(true,true,false,false,invalid,0,0),"Invalid attitude rejected");
   Check(!FlipRecoveryCore.Eligible(true,true,false,false,-1,invalid,0),"Invalid speed rejected");
   Check(!FlipRecoveryCore.Eligible(true,true,false,false,-1,0,invalid),"Invalid spin rejected");
  }
  Console.WriteLine("Flip gate: "+n+" checks passed");
 }
}
