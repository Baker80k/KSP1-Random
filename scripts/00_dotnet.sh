cd "$(dirname "$0")/.."
dotnet build
cp ArchipelagoKSP/bin/Debug/net48/ArchipelagoKSP.dll "/game/steamlibrary/steamapps/common/Kerbal Space Program/GameData/ArchipelagoKSP/Plugins/ArchipelagoKSP.dll"
cp ArchipelagoKSP/bin/Debug/net48/ArchipelagoKSP.pdb "/game/steamlibrary/steamapps/common/Kerbal Space Program/GameData/ArchipelagoKSP/Plugins/ArchipelagoKSP.pdb"