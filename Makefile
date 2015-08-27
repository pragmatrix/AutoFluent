msbuild=msbuild.exe /m /verbosity:m /nologo
nuget=nuget.exe

ver=0.2.0-pre
name=AutoFluent

.PHONY: install
install: build-release
	cp AutoFluent.Command/bin/Release/{*.dll,*.exe,*.config} /usr/local/bin/


.PHONY: build-release
build-release: conf=Release
build-release: build

.PHONY: build
build:
	${msbuild} ${name}.sln /p:Configuration=${conf} /t:"AutoFluent_Command:Rebuild"
	



.PHONY: update-nuget
update-nuget:
	rm -f /usr/local/bin/nuget.exe
	cd /usr/local/bin && wget https://www.nuget.org/nuget.exe && chmod +x nuget.exe
	
