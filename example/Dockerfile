FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

COPY /src/Dosaic.Example.Service/bin/Release/net10.0/publish/ /app
WORKDIR /app

EXPOSE 8080

ENTRYPOINT ["dotnet", "Dosaic.Example.Service.dll"]
