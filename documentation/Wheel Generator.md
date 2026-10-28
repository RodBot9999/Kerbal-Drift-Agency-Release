# Independent wheel configuration generator

The converter writes wheel configs directly from the KDA wheel contract and imported geometry, drivetrain and tire data. It does not read another mod's wheel template. Each generated corner owns the native KSP contact modules and KDA suspension, drive, braking, grip and visual modules.

Stock speed-based auto-steering adjustment is disabled so KDA's controller owns steering response. The required native motor curve is a neutral two-key 2 kNm normalization curve; the KDA engine supplies actual wheel torque in OnDriveUpdate. The generated schema and module order are validated by source tests. Runtime physics is shared through the separately installed base GameData.

Current release checks are in release-manifest.json. The release includes only Lizard GT; parser tests may use external export ZIPs without including those cars in GameData.
