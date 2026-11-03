namespace KerbalDriftAgency {
 public static class InductionCore {
  // Parallel twins: one early response stage, not a sequential second turbo.
  public static void Configure(EngineCore e,int preset){
   e.MechanicalBoost=preset==4;e.ThresholdShape=preset==3?1.45:1;
   e.RotorDecaySeconds=preset==3?1.5:preset==1?.8:1.15;
   e.FlowTaperStartRpm=preset==1?4500:preset==2?6000:preset==0?5500:9000;
   e.FlowTaperEndRpm=preset==1?8000:preset==2?9000:preset==0?9000:10000;
   e.HighRpmBoostFraction=preset==1?.65:preset==2?.88:preset==0?.85:1;
  }
 }
}
