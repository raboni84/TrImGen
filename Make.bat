git fetch exfat-remote master
git subtree pull --prefix ExFat exfat-remote master --squash
dotnet build