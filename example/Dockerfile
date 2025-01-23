FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

COPY /src/Dosaic.Example.Service/bin/Release/net9.0/publish/ /app
WORKDIR /app

EXPOSE 8080

ENTRYPOINT ["dotnet", "Dosaic.Example.Service.dll"]
