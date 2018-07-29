msbuild=msbuild.exe /m /verbosity:m /nologo
nuget=nuget.exe

.PHONY: install
install: build-release
	cp AutoFluent.Command/bin/Release/{*.dll,*.exe,*.config} /usr/local/bin/

.PHONY: build-release
build-release: conf=Release
build-release: build

.PHONY: build
build:
	${msbuild} AutoFluent.sln /p:Configuration=${conf} 

# Xamarin.Forms.AutoFluent

# we want to always stay below the current xamarin forms version to avoid
# confusion.

xfa-ver=3.1.1

.PHONY: package-xfa
package-xfa: ver=${xfa-ver}
package-xfa: name=Xamarin.Forms.AutoFluent
package-xfa: conf=Release
package-xfa: 
	cd ${name} && dotnet pack ${name}.csproj /p:PackageVersion=${ver} -c ${conf}

.PHONY: publish-xfa
publish-xfa: ver=${xfa-ver}
publish-xfa: name=Xamarin.Forms.AutoFluent
publish-xfa: conf=Release
publish-xfa: package-xfa
	cd ${name}/bin/${conf} && nuget push -source https://www.nuget.org/api/v2/package/ ${name}.${ver}.nupkg	



