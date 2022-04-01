To generate the nuget package locally:

nuget.exe pack .\2021\Source\Revit.IFC.Export\BIMOne.Revit2021.IFC.Export.nuspec -NonInteractive -OutputDirectory .\2021 -Properties 'Platform=AnyCPU;Configuration=Release' -version 21.4.0 -Verbosity Detailed

To publish the nuget package locally:

nuget add .\2021\BIMOne.Revit2021.IFC.Export.21.4.0.nupkg -source c:\src\nuget.local