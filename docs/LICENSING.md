# Licence verification and distribution notes

Verified 19 October 2026 for Dad's Route HUD 0.2.0.

## Permission for this source base

**Yes: the source revisions used by this fork carry the MIT licence.** MIT permits copying, modification, merging, publication, redistribution, sublicensing and sale. Forking this source and retaining existing implementation is permitted; reimplementing its features from scratch is not a licence requirement. Include the original copyright and permission notice in copies or substantial portions, including binary distributions. Retain the full licence and disclaimer as supplied. MIT does not require publishing your modifications' source code or obtaining another permission for these licensed acts.

Dad's Route HUD, including its fork additions, is distributed under the MIT terms in the root LICENSE. The original copyright notice is preserved; acknowledging James's fork does not replace upstream attribution. CREDITS.md is ready for inclusion with a download or adaptation into a mod-page credit section.

## Exact evidence

- Original HUD: mspielberg/dv-hud commit `9acf926a2d7c8512ddb85cbf637b3e4a9bfa6f6f`, LICENSE begins `Copyright 2020 Miles Spielberg` and contains the complete MIT grant and disclaimer. https://github.com/mspielberg/dv-hud/blob/9acf926a2d7c8512ddb85cbf637b3e4a9bfa6f6f/LICENSE
- Actual compatibility base: koteq/dv-hud commit `b09fe942ab5118e6aa348a153f686a537a483d0d`, the same full licence text; this is the direct ancestor used before the Dad's Driving HUD snapshot. https://github.com/koteq/dv-hud/blob/b09fe942ab5118e6aa348a153f686a537a483d0d/LICENSE
- QuantitiesNet: submodule commit `2b6522cb70df8108af0d96dec6ebdc6c35a673aa`, repository-root LICENSE begins `Copyright (c) 2021 mspielberg`; the full notice is retained as QuantitiesNet.LICENSE. https://github.com/mspielberg/QuantitiesNet/blob/2b6522cb70df8108af0d96dec6ebdc6c35a673aa/LICENSE

Checked the complete local Git licence blobs at these revisions and compared both distributed licence files, allowing only line-ending differences. Original and compatibility-base HUD notices match. The exact original licence files are retained in the repository and package. The pinned GitHub file URLs could not be fetched. A separate web result for the original repository independently displayed its MIT notice (https://github.com/mspielberg/dv-hud). No fresh Git fetch is claimed. The standard permission/condition was cross-checked against the Open Source Initiative: https://opensource.org/license/mit.

## Package boundaries

Retain LICENSE, QuantitiesNet.LICENSE, THIRD_PARTY_NOTICES.txt and CREDITS.md in the ZIP. Credits alone do not satisfy the full MIT notice condition. The package includes only the fork DLL, its MIT QuantitiesNet dependency, metadata and documentation; game, Unity, UMM, Harmony and optional-mod DLLs/assets are excluded. Local reference assemblies are not relicensed by this project. This public source history omits Koteq's commit containing proprietary lib/ reference binaries; the original MIT history is retained and the compatibility source contributions are credited.

This verification covers the recorded MIT source and dependency. It does not establish permission to redistribute or copy Rinzler's separately distributed implementation. No Rinzler implementation is used in this fork; prior limited declaration/settings comparison is disclosed rather than described as a formal clean-room exercise. It also does not grant another project's trademarks or imply its endorsement. This is a licence/provenance check, not an exhaustive independent originality audit of every implementation detail.
