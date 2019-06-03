# Avalon - Facebook Surveillance 
We are trying to make a library to help defensive squads to take full surveillance capabilities inside Facebook, without using any public API, only the old and good scraping.

> :warning:**Note**: This is a working in progress tool, don't submit a feature request at this time, instead, help us fixing some [issues](https://github.com/0x00000069/avalon/issues) and helping with testing.

### Example

Focusing only on a very simple interface inside our codebase, you can authenticate inside Facebook, like a human does, with a few lines of code:

```csharp
using Avalon;

var gateway = new Gateway("email address", "password");
await gateway.AuthenticateAsync();
```

And create powerful features with our current features:

![Example of Group listing](<https://i.imgur.com/zykZyiN.gif>)

You can **nuke your entire timeline** with just a function! This is a extreme dangerous thing, if you don't have any backup of your persona timeline. Follow the example:

```csharp
var gateway = new Gateway("email address", "password");
await gateway.AuthenticateAsync();

// this can really take a lot of time to execute!
await gateway.NukeAccountAsync();
```

And all of your Facebook posts is gone!

### License

This project is licensed under the terms of the MIT license, and do not have any link with Facebook and/or its brands.