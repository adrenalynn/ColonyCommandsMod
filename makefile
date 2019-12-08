# variables
modname = ColonyCommands
zipname = $(modname)-$(version).zip
dllname = $(modname).dll
version = $(shell cat modInfo.json | awk '/"version"/ {print $$3}' | head -1 | sed 's/[",]//g')
zip_files_extra = announcements.example.json antigrief-config.example.json chatcolors.example.json modInfo.json LICENSE README.md
build_dir = $(modname)
gamedir = /local/games/Steam/steamapps/common/Colony\ Survival

$(dllname): src/*.cs
	mcs /target:library -nostdlib -r:$(gamedir)/colonyserver_Data/Managed/Assembly-CSharp.dll,$(gamedir)/colonyserver_Data/Managed/UnityEngine.CoreModule.dll,$(gamedir)/colonyserver_Data/Managed/mscorlib.dll,$(gamedir)/colonyserver_Data/Managed/System.dll,$(gamedir)/colonyserver_Data/Managed/System.Core.dll,$(gamedir)/colonyserver_Data/Managed/Steamworks.NET.dll,$(gamedir)/colonyserver_Data/Managed/System.IO.Compression.dll,$(gamedir)/colonyserver_Data/Managed/System.IO.Compression.FileSystem.dll -out:"$(dllname)" -sdk:4 src/*.cs

$(zipname): $(dllname) $(zip_files_extra)
	$(RM) $(zipname)
	mkdir -p $(build_dir)
	cp $(dllname) $(zip_files_extra) $(build_dir)/
	zip -r $(zipname) $(build_dir)
	$(RM) -r $(build_dir)

.PHONY: build default clean all zip install serverlog clientlog
build: $(dllname)

default: build

clean:
	$(RM) $(dllname) $(zipname)

all: clean default zip

zip: $(zipname)

install: build checkjson zip
	$(RM) -r $(gamedir)/gamedata/mods/$(build_dir)
	unzip $(zipname) -d $(gamedir)/gamedata/mods

checkjson: *.json
	find . -type f -name "*.json" | while read f; do echo $$f; json_pp <$$f >/dev/null; done

serverlog:
	less $(gamedir)/gamedata/logs/server/$$(ls -1rt $(gamedir)/gamedata/logs/server | tail -1)

clientlog:
	less $(gamedir)/gamedata/logs/client/$$(ls -1rt $(gamedir)/gamedata/logs/client | tail -1)

