# Convenience launchers for the mod. Run from the repo root.
#   make / make start / make user  ->  user mode (normal player UI)
#   make dev                       ->  dev mode (developer tabs)
#   make sandbox                   ->  sandbox mode (user UI + the "Sandbox" tab: roster editor, fish farmer)
#   make build                     ->  compile only, no launch
#
# These wrap the dotnet-native launch profiles in Dark Cloud Improved Version/Properties/launchSettings.json,
# so `dotnet run --launch-profile <user|dev|test>` (or an IDE's run-profile dropdown) does the same thing.

PROJECT := Dark Cloud Improved Version

.PHONY: build start user dev sandbox
start user:
	dotnet run --project "$(PROJECT)" --launch-profile user
dev:
	dotnet run --project "$(PROJECT)" --launch-profile dev
sandbox:
	dotnet run --project "$(PROJECT)" --launch-profile sandbox
build:
	dotnet build "$(PROJECT)"
