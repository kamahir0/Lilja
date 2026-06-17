# #DISPLAY_NAME#

Included in the Lilja package series.

## Installation

1. Open Package Manager from Window > Package Manager.
2. Click the "+" button > Add package from git URL.
3. Enter the following URL:

```text
https://github.com/kamahir0/Lilja.git?path=lilja-packages/#DIRECTORY_NAME#/src/#DISPLAY_NAME#
```

Alternatively, open "Packages/manifest.json" and add the following to the dependencies block:

```json
{
    "dependencies": {
        "#PACKAGE_NAME#": "https://github.com/kamahir0/Lilja.git?path=lilja-packages/#DIRECTORY_NAME#/src/#DISPLAY_NAME#"
    }
}
```


