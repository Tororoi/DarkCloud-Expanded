# Convenience launchers for the mod. Run from the repo root.
#   make / make start / make user  ->  user mode (normal player UI)
#   make dev                       ->  dev mode (developer tabs)
#   make sandbox                   ->  sandbox mode (user UI + the "Sandbox" tab: roster editor, fish farmer)
#   make build                     ->  compile only, no launch
#   make stubs                     ->  reassemble all EE cave stubs (tools/stubs/*.s) into Resources/isoPatch/
#   make stubs-check               ->  verify the committed stub .bins match their .s sources
#   make fishcol                   ->  rebuild every town's fishing collision .bin (tools/build_fishing_collision.py)
#   make fishcol-check             ->  verify those .bins match their generators
#
# These wrap the dotnet-native launch profiles in Dark Cloud Improved Version/Properties/launchSettings.json,
# so `dotnet run --launch-profile <user|dev|test>` (or an IDE's run-profile dropdown) does the same thing.

PROJECT := Dark Cloud Improved Version

.PHONY: build start user dev sandbox stubs stubs-check fishcol fishcol-check
fishcol:
	python3 tools/build_fishing_collision.py
fishcol-check:
	python3 tools/build_fishing_collision.py --check
stubs:
	python3 tools/stubs/build_ee_stubs.py
stubs-check:
	python3 tools/stubs/build_ee_stubs.py --check
start user:
	dotnet run --project "$(PROJECT)" --launch-profile user
dev:
	dotnet run --project "$(PROJECT)" --launch-profile dev
sandbox:
	dotnet run --project "$(PROJECT)" --launch-profile sandbox
build:
	dotnet build "$(PROJECT)"
