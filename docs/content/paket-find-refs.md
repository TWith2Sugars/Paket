# paket find-refs

Finds all project files that have the given NuGet packages installed.

    [lang=batchfile]
    $ paket find-refs PACKAGENAME1 PACKAGENAME1 ...

## Sample

*.src/Paket/paket.references* contains:

	UnionArgParser
	FSharp.Core

*.src/Paket.Core/paket.references* contains:

	Newtonsoft.Json
	DotNetZip
	FSharp.Core

Now we run
	
	paket find-refs DotNetZip FSharp.Core

and paket gives the following output:
	
	DotNetZip
	.src/Paket.Core/Paket.Core.fsproj

	FSharp.Core
	.src/Paket.Core/Paket.Core.fsproj
	.src/Paket/Paket.fsproj