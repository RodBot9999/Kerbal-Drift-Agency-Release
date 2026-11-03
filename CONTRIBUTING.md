# Contributing to Kerbal Drift Agency

Community fixes and ideas are welcome. @RodBot9999 reviews contributions and
decides what is merged and released; see [project governance](GOVERNANCE.md).

## Report an issue or suggest a feature

Search existing issues first. For a bug, include your KSP/KDA versions, relevant
mods, reproduction steps, expected behavior and what happened. Attach relevant
log excerpts or telemetry when useful. Remove personal information and secrets
from attachments. Discuss large features or new dependencies in an issue before
spending substantial time on an implementation.

## Submit a change

1. Fork the repository and create a branch for one focused change.
2. Follow the [source and build instructions](source/README.md). Keep unrelated
   edits and generated files out of the change unless needed for the fix.
3. Run checks appropriate to the change. Explain what you tested, the results
   and any limits; include screenshots for visible changes when practical.
4. Open a pull request against `main`. Describe the problem and resulting
   behavior, link any related issue, and identify new dependencies or assets.
5. Respond to review feedback. Updated code may need a new approval. The owner
   decides whether and when to merge and publish it.

Community contributions use forks and pull requests, not direct pushes to the
official repository. Submitting a contribution does not grant repository access
or authority to publish official releases.

## Contribution rights and licenses

You retain copyright in your contribution. By intentionally submitting it for
inclusion, you agree to license your original contribution under the existing
license applicable to the component you modify, as described in
[LICENSE.md](LICENSE.md) and [ASSET-LICENSES.md](ASSET-LICENSES.md). This is not a
copyright assignment and does not give the project permission to relicense other
people's work. Retain applicable copyright, attribution and license notices.

KDA is a multi-license distribution: runtime/ECU code is MIT, converter software
is GPL-3.0-only, and original audio/manual text is CC BY 4.0, with exceptions and
separate asset terms detailed in the license map. Do not assume that the whole
repository, all artwork or all vendor code has one license. If a new file's
license scope is unclear, discuss it with the owner before submission.

Only submit material you authored or have permission to contribute under terms
compatible with its intended distribution. Do not submit proprietary game code,
unlicensed assets, credentials or material copied from private projects without
authorization. Disclose reused third-party code and retain its upstream license.

## Models, textures, sounds and other assets

For each new or replaced asset, include in the pull request:

- Its author/rightsholder and original source URL, or an explicit statement that
  you created it yourself, plus the editable source where available.
- The exact license and a copy/link to its terms, required attribution, and any
  separate written permission needed for redistribution or modification.
- What was modified and which files use the asset. Disclose AI assistance and
  any third-party inputs when relevant to the asset's provenance.
- Confirmation that the permission covers the actual files being distributed,
  including editable model/source files if included, not only rendered images
  or private use.

Free to download does not by itself mean free to redistribute. Do not remove
notices or label third-party artwork as original KDA work. Assets without clear
provenance and suitable permissions cannot be accepted until that is resolved.

## Respectful participation

Keep discussions constructive, relevant and respectful. The owner may close
off-topic or duplicate requests and moderate harassment or spam. These community
rules govern participation here and do not remove rights granted by licenses.
