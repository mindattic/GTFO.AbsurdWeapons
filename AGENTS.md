# MindAttic project agent entrypoint

Read the shared protocol at ..\mindattic-agent-standard\AGENTS.md and this project's own
documentation. Common prompt commands are implemented by the shared runner; do not add a second
copy under a provider-specific command folder.

## Project context

This repo is GTFO.EZ.AbsurdWeapons, a GTFO.EZ companion mod (formerly GTFO.AbsurdWeapons; the local folder may still
be named that until it's renamed). The shared code (SharedOwner, the Insert/Delete toggles,
MessageHelper, ...) is compiled in from the GTFO.EZ.Shared git submodule at `external/GTFO.EZ.Shared`
(linked in `src/GTFO.EZ.AbsurdWeapons/GTFO.EZ.AbsurdWeapons.csproj`) - change it in the GTFO.EZ.Shared repo, then move the submodule.
