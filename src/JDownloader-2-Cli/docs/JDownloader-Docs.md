# My.JDownloader API Documentation

(Converted from https://my.jdownloader.org/developers/index.html)

Basics
------

This document specifies the development state of the MyJDownloader API. As the API changes regularly, changes in this document will happen every so often. The API is designed to offer a secure communication between the JDownloader client and the request client, and to prevent any man in the middle listeners. For this approach the API is using AES128CBC and HMAC-SHA256. To be able to communicate with the API we would suggest to make sure you understand the procedure as this can get somewhat complicated for beginners. The API is REST based, but currently only GET and POST routes are offered.

API Endpoint: https://api.jdownloader.org  
To avoid problems with NAT and Portforwarding, the communication between the frontends and JDownloader is routed through our Connection-Server. IF available, the frontend will try to establish a direct connection between the frontend and JDownloader after validating the account credentials and initializing the encryption tokens.  
Pro Tip: It's possible to access the JDownloader API directly (Bypass our server) by enabling the so called 'Deprecated API' in the Advanced Options.

If things do not work as expected, please check the common mistakes section first.

### Parameter Order

The parameter in GET/POST requests are important. Use the documented order or you won't get a positive response.

### Content-Type Header & JSON Syntax

Make sure to use the correct Content-Type. For most calls, this is application/json; charset=utf-8, see call description

The API expects JSON Input and will return JSON output. Make sure that you send valid JSON formatted text.  
We highly recommend, that you use a JSON Library to create proper JSON, and parse the results. The results syntax of existing methods MAY CHANGE, but the content structure and field names will remain. (e.g. we might add a field, or shuffle fields, but we will not rename or remove an existing field. This way, the API will stay compatible)

### Always provide a new RequestID

The RequestID is required in almost every request. It's a number that has to increase from one call to another. You can either use a millisecond precise timestamp, or a self incrementing number. The API will return the RequestID in the response. **You should validate the response to make sure the answer is valid.**

### Field names

Make sure that you use the correct field names.

```
WRONG!
	/downloadsV2/queryLinks?{"packageUUID":[1468427395088]}
	
```


```
CORRECT!
	/downloadsV2/queryLinks?{"packageUUIDs":[1468427395088]}
	
```


### URL-Encoding

Make sure all url parameters are correctly urlencoded.

Most calls require a signature that validates the call against our server and your JDownloader. The API will only accept calls with a proper signature.  
Create the Signature:

1.  build the full queryString (incl. RequestID)
2.  hmac the queryString. The used Key depends. Some calls use serverEncryptionToken, others have to ask the user for email and password, create the loginSecret and use the loginsecret as key. email needs to be lower case!
3.  hexformat the result
4.  append the signature to the queryString &signature=.

```
Example:
queryString = "/my/connect?email=foo@bar.com&rid=1361982773157";
queryString += "&signature=" + HmacSha256(utf8bytes(queryString), ServerEncryptionToken);


```


All HTTP Response codes except 200 are errors or exceptions. The response content contains an ErrorObject in this case. Errors can have different origins. Depending on the call, the Connection Server or the JDownloader installation might throw an error. Check the 'src' field to get the origin.

```
{
"src":"MYJD"|"DEVICE"  
"type":<see errortypes below>
"data":<Optional Data Object>
}


```


### Device Error Types

Device Errors' origin is a JDownloader installation.

```
"src":"DEVICE"
```


*   SESSION
*   API\_COMMAND\_NOT\_FOUND
*   AUTH\_FAILED
*   FILE\_NOT\_FOUND
*   INTERNAL\_SERVER\_ERROR
*   API\_INTERFACE\_NOT\_FOUND
*   BAD\_PARAMETERS

### Server Error Types

Server Errors' origin is the Connection Server

```
"src":"MYJD"
```


*   MAINTENANCE
*   OVERLOAD
*   TOO\_MANY\_REQUESTS
*   ERROR\_EMAIL\_NOT\_CONFIRMED
*   OUTDATED
*   TOKEN\_INVALID
*   OFFLINE
*   UNKNOWN
*   BAD\_REQUEST
*   AUTH\_FAILED
*   EMAIL\_INVALID
*   CHALLENGE\_FAILED
*   METHOD\_FORBIDDEN
*   EMAIL\_FORBIDDEN
*   FAILED
*   STORAGE\_NOT\_FOUND
*   STORAGE\_LIMIT\_REACHED
*   STORAGE\_ALREADY\_EXISTS
*   STORAGE\_INVALID\_KEY
*   STORAGE\_KEY\_NOT\_FOUND
*   STORAGE\_INVALID\_STORAGEID

Account Management
------------------

### 1\. Get Captcha Challenge

*   Call /captcha/getCaptcha
*   Return type /captcha/getCaptcha
*   Possible Errors OVERLOAD
*     MAINTENANCE
*     TOO\_MANY\_REQUESTS

Response Content

```
{ 
"captchaChallenge" :	"13650058317(...)8299d97cf5",
"image" : 		"data:image/png;base64,iVBORw0KGgoCAYAA(...)"
}

```


### 2\. Register

### 3\. Finish Registration

Methods
-------

### flashParameter: 0

*   Call /flash
*   Possible Error(s) ResponseCode 500 (Internal Server Error); Type:INTERNAL\_SERVER\_ERROR

### crossdomain.xmlParameter: 0

*   Call /crossdomain.xml
*   Possible Error(s) ResponseCode 500 (Internal Server Error); Type:INTERNAL\_SERVER\_ERROR

### favicon.icoParameter: 0

*   Call /favicon.ico
*   Possible Error(s) ResponseCode 500 (Internal Server Error); Type:INTERNAL\_SERVER\_ERROR

### flashgotParameter: 0

*   Call /flashgot
*   Possible Error(s) ResponseCode 500 (Internal Server Error); Type:INTERNAL\_SERVER\_ERROR

### jdcheck.jsParameter: 0

*   Call /jdcheck.js
*   Possible Error(s) ResponseCode 500 (Internal Server Error); Type:INTERNAL\_SERVER\_ERROR

### jdcheckjsonParameter: 0

*   Call /jdcheckjson
*   Possible Error(s) ResponseCode 500 (Internal Server Error); Type:INTERNAL\_SERVER\_ERROR

### addAccountParameter: 3

*   Parameter 1 - premiumHoster (String)
*     2 - username (String)
*     3 - password (String)
*   Call /accounts/addAccount?premiumHoster&username&password
*   Return type boolean

### disableAccountsParameter: 1

*   Parameter 1 - ids (List)
*   Call /accounts/disableAccounts?ids
*   Return type boolean

### enableAccountsParameter: 1

*   Parameter 1 - ids (List)
*   Call /accounts/enableAccounts?ids
*   Return type boolean

### getAccountInfoParameter: 1

*   Parameter 1 - id (long)
*   Call /accounts/getAccountInfo?id
*   Return type [Account](#tag_12)

### getPremiumHosterUrlParameter: 1

*   Parameter 1 - hoster (String)
*   Call /accounts/getPremiumHosterUrl?hoster
*   Return type String

### listPremiumHosterParameter: 0

*   Call /accounts/listPremiumHoster
*   Return type List<String>

### listPremiumHosterUrlsParameter: 0

*   Call /accounts/listPremiumHosterUrls
*   Return type JsonMap

### premiumHosterIconParameter: 1

*   Parameter 1 - premiumHoster (String)
*   Call /accounts/premiumHosterIcon?premiumHoster
*   Possible Error(s) ResponseCode 500 (Internal Server Error); Type:INTERNAL\_SERVER\_ERROR

### queryAccountsParameter: 1

*   Parameter 1 - query ([APIQuery](#tag_18))
*   Call /accounts/queryAccounts?query
*   Return type List<[Account](#tag_12)\>

### removeAccountsParameter: 1

*   Parameter 1 - ids (Long\[\])
*   Call /accounts/removeAccounts?ids
*   Return type boolean

### setEnabledStateParameter: 2

*   Parameter 1 - enabled (boolean)
*     2 - ids (Long\[\])
*   Call /accounts/setEnabledState?enabled&ids
*   Return type boolean

### updateAccountParameter: 3

*   Parameter 1 - accountId (Long)
*     2 - username (String)
*     3 - password (String)
*   Call /accounts/updateAccount?accountId&username&password
*   Return type boolean

### addAccountParameter: 3

*   Parameter 1 - premiumHoster (String)
*     2 - username (String)
*     3 - password (String)
*   Call /accountsV2/addAccount?premiumHoster&username&password

### addBasicAuthParameter: 4

*   Parameter 1 - type ([Type](#tag_25))
*     2 - hostmask (String)
*     3 - username (String)
*     4 - password (String)
*   Call /accountsV2/addBasicAuth?type&hostmask&username&password
*   Return type long
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### disableAccountsParameter: 1

*   Parameter 1 - ids (long\[\])
*   Call /accountsV2/disableAccounts?ids

### enableAccountsParameter: 1

*   Parameter 1 - ids (long\[\])
*   Call /accountsV2/enableAccounts?ids

### getPremiumHosterUrlParameter: 1

*   Parameter 1 - hoster (String)
*   Call /accountsV2/getPremiumHosterUrl?hoster
*   Return type String

### listAccountsParameter: 1

*   Parameter 1 - query ([AccountQuery](#tag_31))
*   Call /accountsV2/listAccounts?query
*   Return type List<[Account](#tag_30)\>

### listBasicAuthParameter: 0

*   Call /accountsV2/listBasicAuth
*   Return type List<[BasicAuthentication](#tag_33)\>

### listPremiumHosterParameter: 0

*   Call /accountsV2/listPremiumHoster
*   Return type List<String>

### listPremiumHosterUrlsParameter: 0

*   Call /accountsV2/listPremiumHosterUrls
*   Return type Map<String, String>

### refreshAccountsParameter: 1

*   Parameter 1 - ids (long\[\])
*   Call /accountsV2/refreshAccounts?ids

### removeAccountsParameter: 1

*   Parameter 1 - ids (long\[\])
*   Call /accountsV2/removeAccounts?ids

### removeBasicAuthsParameter: 1

*   Parameter 1 - ids (long\[\])
*   Call /accountsV2/removeBasicAuths?ids
*   Return type boolean
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### setUserNameAndPasswordParameter: 3

*   Parameter 1 - accountId (long)
*     2 - username (String)
*     3 - password (String)
*   Call /accountsV2/setUserNameAndPassword?accountId&username&password
*   Return type boolean

### updateBasicAuthParameter: 1

*   Parameter 1 - updatedEntry ([BasicAuthentication](#tag_33))
*   Call /accountsV2/updateBasicAuth?updatedEntry
*   Return type boolean
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### getParameter: 1

*   Parameter 1 - id (long)
*   Description Returns Captcha Image as Base64 encoded data url
*   Call /captcha/get?id
*   Possible Error(s) ResponseCode 500 (Internal Server Error); Type:INTERNAL\_SERVER\_ERROR
*     ResponseCode 404 (Not Found); Type:NOT\_AVAILABLE

### getParameter: 2

*   Parameter 1 - id (long)
*     2 - format (String)
*   Description Returns Captcha Image as Base64 encoded data url
*   Call /captcha/get?id&format
*   Possible Error(s) ResponseCode 500 (Internal Server Error); Type:INTERNAL\_SERVER\_ERROR
*     ResponseCode 404 (Not Found); Type:NOT\_AVAILABLE

### getCaptchaJobParameter: 1

*   Parameter 1 - id (long)
*   Description Returns CaptchaJob Object for the given id
*   Call /captcha/getCaptchaJob?id
*   Return type [CaptchaJob](#tag_45)
*   Possible Error(s) ResponseCode 404 (Not Found); Type:NOT\_AVAILABLE

### listParameter: 0

*   Description returns a list of all available captcha jobs
*   Call /captcha/list
*   Return type List<[CaptchaJob](#tag_45)\>

### skipParameter: 2

*   Parameter 1 - id (long)
*     2 - type ([SkipRequest](#tag_48))
*   Call /captcha/skip?id&type
*   Return type boolean
*   Possible Error(s) ResponseCode 404 (Not Found); Type:NOT\_AVAILABLE

### skipParameter: 1

*   DEPRECATED Method. This method will be removed soon. DO NOT USE IT!
    
*   Parameter 1 - id (long)
*   Call /captcha/skip?id
*   Return type boolean
*   Possible Error(s) ResponseCode 404 (Not Found); Type:NOT\_AVAILABLE

### solveParameter: 3

*   Parameter 1 - id (long)
*     2 - result (String)
*     3 - resultFormat (String)
*   Call /captcha/solve?id&result&resultFormat
*   Return type boolean
*   Possible Error(s) ResponseCode 404 (Not Found); Type:NOT\_AVAILABLE
*     ResponseCode 404 (Not Found); Type:UNKNOWN\_CHALLENGETYPE; Description:

### solveParameter: 2

*   Parameter 1 - id (long)
*     2 - result (String)
*   Call /captcha/solve?id&result
*   Return type boolean
*   Possible Error(s) ResponseCode 404 (Not Found); Type:NOT\_AVAILABLE
*     ResponseCode 404 (Not Found); Type:UNKNOWN\_CHALLENGETYPE; Description:

### createJobRecaptchaV2Parameter: 4

*   Parameter 1 - String
*     2 - String
*     3 - String
*     4 - String
*   Call /captchaforward/createJobRecaptchaV2?String&String&String&String
*   Return type long

### getResultParameter: 1

*   Parameter 1 - long
*   Call /captchaforward/getResult?long
*   Return type String
*   Possible Error(s) ResponseCode 404 (Not Found); Type:Not Found
*     ResponseCode 500 (Internal Server Error); Type:INTERNAL\_SERVER\_ERROR

### getParameter: 3

*   Parameter 1 - interfaceName (String)
*     2 - storage (String)
*     3 - key (String)
*   Description get value from interface by key
*   Call /config/get?interfaceName&storage&key
*   Return type Object

### getDefaultParameter: 3

*   Parameter 1 - interfaceName (String)
*     2 - storage (String)
*     3 - key (String)
*   Description get default value from interface by key
*   Call /config/getDefault?interfaceName&storage&key
*   Return type Object

### listParameter: 0

*   Description list all available config entries
*   Call /config/list
*   Return type List<[AdvancedConfigAPIEntry](#tag_60)\>

### listParameter: 5

*   Parameter 1 - pattern (String)
*     2 - returnDescription (boolean)
*     3 - returnValues (boolean)
*     4 - returnDefaultValues (boolean)
*     5 - returnEnumInfo (boolean)
*   Description list entries based on the pattern regex
*   Call /config/list?pattern&returnDescription&returnValues&returnDefaultValues&returnEnumInfo
*   Return type List<[AdvancedConfigAPIEntry](#tag_60)\>

### listParameter: 4

*   DEPRECATED Method. This method will be removed soon. DO NOT USE IT!
    
*   Parameter 1 - pattern (String)
*     2 - returnDescription (boolean)
*     3 - returnValues (boolean)
*     4 - returnDefaultValues (boolean)
*   Description DEPRECATED! list entries based on the pattern regex
*   Call /config/list?pattern&returnDescription&returnValues&returnDefaultValues
*   Return type List<[AdvancedConfigAPIEntry](#tag_60)\>

### listEnumParameter: 1

*   Parameter 1 - type (String)
*   Description list all possible enum values
*   Call /config/listEnum?type
*   Return type List<[EnumOption](#tag_64)\>
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### queryParameter: 1

*   Parameter 1 - query ([AdvancedConfigQuery](#tag_66))
*   Call /config/query?query
*   Return type List<[AdvancedConfigAPIEntry](#tag_60)\>

### resetParameter: 3

*   Parameter 1 - interfaceName (String)
*     2 - storage (String)
*     3 - key (String)
*   Description reset interface by key to its default value
*   Call /config/reset?interfaceName&storage&key
*   Return type boolean

### setParameter: 4

*   Parameter 1 - interfaceName (String)
*     2 - storage (String)
*     3 - key (String)
*     4 - value (Object)
*   Description set value to interface by key
*   Call /config/set?interfaceName&storage&key&value
*   Return type boolean
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:INVALID\_VALUE

### faviconParameter: 1

*   DEPRECATED Method. This method will be removed soon. DO NOT USE IT!
    
*   Parameter 1 - hostername (String)
*   Call /content/favicon?hostername
*   Possible Error(s) FileNotFoundException
*     ResponseCode 500 (Internal Server Error); Type:INTERNAL\_SERVER\_ERROR

### fileIconParameter: 1

*   DEPRECATED Method. This method will be removed soon. DO NOT USE IT!
    
*   Parameter 1 - filename (String)
*   Call /content/fileIcon?filename
*   Possible Error(s) ResponseCode 500 (Internal Server Error); Type:INTERNAL\_SERVER\_ERROR

### getFavIconParameter: 1

*   Parameter 1 - hostername (String)
*   Call /contentV2/getFavIcon?hostername
*   Possible Error(s) ResponseCode 404 (Not Found); Type:FILE\_NOT\_FOUND
*     ResponseCode 500 (Internal Server Error); Type:INTERNAL\_SERVER\_ERROR

### getFileIconParameter: 1

*   Parameter 1 - filename (String)
*   Call /contentV2/getFileIcon?filename
*   Possible Error(s) ResponseCode 500 (Internal Server Error); Type:INTERNAL\_SERVER\_ERROR

### getIconParameter: 2

*   Parameter 1 - key (String)
*     2 - size (int)
*   Call /contentV2/getIcon?key&size
*   Possible Error(s) ResponseCode 500 (Internal Server Error); Type:INTERNAL\_SERVER\_ERROR
*     ResponseCode 404 (Not Found); Type:FILE\_NOT\_FOUND
*     ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS

### getIconDescriptionParameter: 1

*   Parameter 1 - key (String)
*   Call /contentV2/getIconDescription?key
*   Return type [IconDescriptor](#tag_77)
*   Possible Error(s) ResponseCode 500 (Internal Server Error); Type:INTERNAL\_SERVER\_ERROR

### getDirectConnectionInfosParameter: 0

*   Call /device/getDirectConnectionInfos
*   Return type DirectConnectionInfos

### getSessionPublicKeyParameter: 0

*   Call /device/getSessionPublicKey
*   Return type String

### pingParameter: 0

*   Call /device/ping
*   Return type boolean

### answerParameter: 2

*   Parameter 1 - id (long)
*     2 - data (Map)
*   Call /dialogs/answer?id&data
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:Answer Dialog 0 first
*     ResponseCode 400 (Bad Request); Type:Invalid ID: 0

### getParameter: 3

*   Parameter 1 - id (long)
*     2 - icon (boolean)
*     3 - properties (boolean)
*   Call /dialogs/get?id&icon&properties
*   Return type [DialogInfo](#tag_85)
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:Invalid ID: 0

### getTypeInfoParameter: 1

*   Parameter 1 - dialogType (String)
*   Call /dialogs/getTypeInfo?dialogType
*   Return type [DialogTypeInfo](#tag_87)
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:Requested Type not found!

### listParameter: 0

*   Call /dialogs/list
*   Return type long\[\]

### forceDownloadParameter: 2

*   Parameter 1 - linkIds (long\[\])
*     2 - packageIds (long\[\])
*   Call /downloadcontroller/forceDownload?linkIds&packageIds

### getCurrentStateParameter: 0

*   Call /downloadcontroller/getCurrentState
*   Return type String

### getSpeedInBpsParameter: 0

*   Call /downloadcontroller/getSpeedInBps
*   Return type int

### pauseParameter: 1

*   Parameter 1 - value (boolean)
*   Call /downloadcontroller/pause?value
*   Return type boolean

### startParameter: 0

*   Call /downloadcontroller/start
*   Return type boolean

### stopParameter: 0

*   Call /downloadcontroller/stop
*   Return type boolean

### queryLinksParameter: 2

*   Parameter 1 - queryParams ([LinkQuery](#tag_98))
*     2 - diffID (int)
*   Call /downloadevents/queryLinks?queryParams&diffID
*   Return type [DownloadListDiff](#tag_99)
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### setStatusEventIntervalParameter: 2

*   Parameter 1 - channelID (long)
*     2 - interval (long)
*   Call /downloadevents/setStatusEventInterval?channelID&interval
*   Return type boolean

### disableLinksParameter: 2

*   Parameter 1 - linkIds (List)
*     2 - packageIds (List)
*   Call /downloads/disableLinks?linkIds&packageIds
*   Return type boolean

### disableLinksParameter: 1

*   Parameter 1 - linkIds (List)
*   Call /downloads/disableLinks?linkIds
*   Return type boolean

### enableLinksParameter: 2

*   Parameter 1 - linkIds (List)
*     2 - packageIds (List)
*   Call /downloads/enableLinks?linkIds&packageIds
*   Return type boolean

### enableLinksParameter: 1

*   Parameter 1 - linkIds (List)
*   Call /downloads/enableLinks?linkIds
*   Return type boolean

### forceDownloadParameter: 2

*   Parameter 1 - linkIds (List)
*     2 - packageIds (List)
*   Call /downloads/forceDownload?linkIds&packageIds
*   Return type boolean

### forceDownloadParameter: 1

*   Parameter 1 - linkIds (List)
*   Call /downloads/forceDownload?linkIds
*   Return type boolean

### getChildrenChangedParameter: 1

*   Parameter 1 - structureWatermark (Long)
*   Call /downloads/getChildrenChanged?structureWatermark
*   Return type Long

### getJDStateParameter: 0

*   Call /downloads/getJDState
*   Return type String

### moveLinksParameter: 1

*   Parameter 1 - query ([APIQuery](#tag_18))
*   Call /downloads/moveLinks?query
*   Return type boolean

### movePackagesParameter: 1

*   Parameter 1 - query ([APIQuery](#tag_18))
*   Call /downloads/movePackages?query
*   Return type boolean

### packageCountParameter: 0

*   Call /downloads/packageCount
*   Return type int

### pauseParameter: 1

*   Parameter 1 - value (boolean|null)
*   Call /downloads/pause?value
*   Return type boolean

### queryLinksParameter: 1

*   Parameter 1 - queryParams ([APIQuery](#tag_18))
*   Call /downloads/queryLinks?queryParams
*   Return type List<[DownloadLink](#tag_115)\>

### queryPackagesParameter: 1

*   Parameter 1 - queryParams ([APIQuery](#tag_18))
*   Call /downloads/queryPackages?queryParams
*   Return type List<[FilePackage](#tag_117)\>

### removeLinksParameter: 2

*   Parameter 1 - linkIds (List)
*     2 - packageIds (List)
*   Call /downloads/removeLinks?linkIds&packageIds
*   Return type boolean

### removeLinksParameter: 1

*   Parameter 1 - linkIds (List)
*   Call /downloads/removeLinks?linkIds
*   Return type boolean

### renamePackageParameter: 2

*   Parameter 1 - packageId (Long)
*     2 - newName (String)
*   Call /downloads/renamePackage?packageId&newName
*   Return type boolean

### resetLinksParameter: 1

*   Parameter 1 - linkIds (List)
*   Call /downloads/resetLinks?linkIds
*   Return type boolean

### resetLinksParameter: 2

*   Parameter 1 - linkIds (List)
*     2 - packageIds (List)
*   Call /downloads/resetLinks?linkIds&packageIds
*   Return type boolean

### speedParameter: 0

*   Call /downloads/speed
*   Return type int

### startParameter: 0

*   Call /downloads/start
*   Return type boolean

### stopParameter: 0

*   Call /downloads/stop
*   Return type boolean

### cleanupParameter: 5

*   Parameter 1 - linkIds (long\[\])
*     2 - packageIds (long\[\])
*     3 - action ([Action](#tag_128))
*     4 - mode ([Mode](#tag_129))
*     5 - selectionType ([SelectionType](#tag_130))
*   Call /downloadsV2/cleanup?linkIds&packageIds&action&mode&selectionType
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### forceDownloadParameter: 2

*   Parameter 1 - linkIds (long\[\])
*     2 - packageIds (long\[\])
*   Call /downloadsV2/forceDownload?linkIds&packageIds
*   Return type boolean
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### getDownloadUrlsParameter: 3

*   Parameter 1 - linkIds (long\[\])
*     2 - packageIds (long\[\])
*     3 - urlDisplayType (UrlDisplayTypeStorable\[\])
*   Call /downloadsV2/getDownloadUrls?linkIds&packageIds&urlDisplayType
*   Return type Map<String, List<Long>>
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### getStopMarkParameter: 0

*   Call /downloadsV2/getStopMark
*   Return type long

### getStopMarkedLinkParameter: 0

*   Call /downloadsV2/getStopMarkedLink
*   Return type [DownloadLink](#tag_135)

### getStructureChangeCounterParameter: 1

*   Parameter 1 - oldCounterValue (long)
*   Call /downloadsV2/getStructureChangeCounter?oldCounterValue
*   Return type long

### moveLinksParameter: 3

*   Parameter 1 - linkIds (long\[\])
*     2 - afterLinkID (long)
*     3 - destPackageID (long)
*   Call /downloadsV2/moveLinks?linkIds&afterLinkID&destPackageID

### movePackagesParameter: 2

*   Parameter 1 - packageIds (long\[\])
*     2 - afterDestPackageId (long)
*   Call /downloadsV2/movePackages?packageIds&afterDestPackageId

### movetoNewPackageParameter: 4

*   Parameter 1 - linkIds (long\[\])
*     2 - pkgIds (long\[\])
*     3 - newPkgName (String)
*     4 - downloadPath (String)
*   Call /downloadsV2/movetoNewPackage?linkIds&pkgIds&newPkgName&downloadPath
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### packageCountParameter: 0

*   Call /downloadsV2/packageCount
*   Return type int

### queryLinksParameter: 1

*   Parameter 1 - queryParams ([LinkQuery](#tag_98))
*   Call /downloadsV2/queryLinks?queryParams
*   Return type List<[DownloadLink](#tag_135)\>
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### queryPackagesParameter: 1

*   Parameter 1 - queryParams ([PackageQuery](#tag_144))
*   Call /downloadsV2/queryPackages?queryParams
*   Return type List<[FilePackage](#tag_145)\>
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### removeLinksParameter: 2

*   Parameter 1 - linkIds (long\[\])
*     2 - packageIds (long\[\])
*   Call /downloadsV2/removeLinks?linkIds&packageIds
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### removeStopMarkParameter: 0

*   Call /downloadsV2/removeStopMark

### renameLinkParameter: 2

*   Parameter 1 - linkId (Long)
*     2 - newName (String)
*   Call /downloadsV2/renameLink?linkId&newName

### renamePackageParameter: 2

*   Parameter 1 - packageId (Long)
*     2 - newName (String)
*   Call /downloadsV2/renamePackage?packageId&newName

### resetLinksParameter: 2

*   Parameter 1 - linkIds (long\[\])
*     2 - packageIds (long\[\])
*   Call /downloadsV2/resetLinks?linkIds&packageIds

### resumeLinksParameter: 2

*   Parameter 1 - linkIds (long\[\])
*     2 - packageIds (long\[\])
*   Call /downloadsV2/resumeLinks?linkIds&packageIds
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### setDownloadDirectoryParameter: 2

*   Parameter 1 - directory (String)
*     2 - packageIds (long\[\])
*   Call /downloadsV2/setDownloadDirectory?directory&packageIds

### setDownloadPasswordParameter: 3

*   Parameter 1 - linkIds (long\[\])
*     2 - packageIds (long\[\])
*     3 - pass (String)
*   Call /downloadsV2/setDownloadPassword?linkIds&packageIds&pass
*   Return type boolean
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### setEnabledParameter: 3

*   Parameter 1 - enabled (boolean)
*     2 - linkIds (long\[\])
*     3 - packageIds (long\[\])
*   Call /downloadsV2/setEnabled?enabled&linkIds&packageIds

### setPriorityParameter: 3

*   Parameter 1 - priority ([Priority](#tag_136))
*     2 - linkIds (long\[\])
*     3 - packageIds (long\[\])
*   Call /downloadsV2/setPriority?priority&linkIds&packageIds
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### setStopMarkParameter: 2

*   Parameter 1 - linkId (long)
*     2 - packageId (long)
*   Call /downloadsV2/setStopMark?linkId&packageId

### splitPackageByHosterParameter: 2

*   Parameter 1 - linkIds (long\[\])
*     2 - pkgIds (long\[\])
*   Call /downloadsV2/splitPackageByHoster?linkIds&pkgIds

### startOnlineStatusCheckParameter: 2

*   Parameter 1 - linkIds (long\[\])
*     2 - packageIds (long\[\])
*   Call /downloadsV2/startOnlineStatusCheck?linkIds&packageIds
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### unskipParameter: 3

*   Parameter 1 - packageIds (long\[\])
*     2 - linkIds (long\[\])
*     3 - filterByReason ([Reason](#tag_160))
*   Call /downloadsV2/unskip?packageIds&linkIds&filterByReason
*   Return type boolean
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### addsubscriptionParameter: 3

*   Parameter 1 - subscriptionid (long)
*     2 - subscriptions (String\[\])
*     3 - exclusions (String\[\])
*   Call /events/addsubscription?subscriptionid&subscriptions&exclusions
*   Return type [SubscriptionResponse](#tag_163)

### changesubscriptiontimeoutsParameter: 3

*   Parameter 1 - subscriptionid (long)
*     2 - polltimeout (long)
*     3 - maxkeepalive (long)
*   Call /events/changesubscriptiontimeouts?subscriptionid&polltimeout&maxkeepalive
*   Return type [SubscriptionResponse](#tag_163)

### getsubscriptionParameter: 1

*   Parameter 1 - subscriptionid (long)
*   Call /events/getsubscription?subscriptionid
*   Return type [SubscriptionResponse](#tag_163)

### getsubscriptionstatusParameter: 1

*   Parameter 1 - subscriptionid (long)
*   Call /events/getsubscriptionstatus?subscriptionid
*   Return type [SubscriptionStatusResponse](#tag_167)

### listenParameter: 1

*   Parameter 1 - subscriptionid (long)
*   Call /events/listen?subscriptionid
*   Possible Error(s) ResponseCode 404 (Not Found); Type:FILE\_NOT\_FOUND
*     ResponseCode 500 (Internal Server Error); Type:INTERNAL\_SERVER\_ERROR

### listpublisherParameter: 0

*   Call /events/listpublisher
*   Return type List<[PublisherResponse](#tag_170)\>

### removesubscriptionParameter: 3

*   Parameter 1 - subscriptionid (long)
*     2 - subscriptions (String\[\])
*     3 - exclusions (String\[\])
*   Call /events/removesubscription?subscriptionid&subscriptions&exclusions
*   Return type [SubscriptionResponse](#tag_163)

### setsubscriptionParameter: 3

*   Parameter 1 - subscriptionid (long)
*     2 - subscriptions (String\[\])
*     3 - exclusions (String\[\])
*   Call /events/setsubscription?subscriptionid&subscriptions&exclusions
*   Return type [SubscriptionResponse](#tag_163)

### subscribeParameter: 2

*   Parameter 1 - subscriptions (String\[\])
*     2 - exclusions (String\[\])
*   Call /events/subscribe?subscriptions&exclusions
*   Return type [SubscriptionResponse](#tag_163)

### unsubscribeParameter: 1

*   Parameter 1 - subscriptionid (long)
*   Call /events/unsubscribe?subscriptionid
*   Return type [SubscriptionResponse](#tag_163)

### installParameter: 1

*   Parameter 1 - id (String)
*   Call /extensions/install?id
*   Return type boolean

### isEnabledParameter: 1

*   Parameter 1 - classname (String)
*   Call /extensions/isEnabled?classname
*   Return type boolean

### isInstalledParameter: 1

*   Parameter 1 - id (String)
*   Call /extensions/isInstalled?id
*   Return type boolean

### listParameter: 1

*   Parameter 1 - query ([ExtensionQuery](#tag_181))
*   Call /extensions/list?query
*   Return type List<[Extension](#tag_180)\>

### setEnabledParameter: 2

*   Parameter 1 - classname (String)
*     2 - b (boolean)
*   Call /extensions/setEnabled?classname&b
*   Return type boolean

### addArchivePasswordParameter: 1

*   Parameter 1 - password (String)
*   Call /extraction/addArchivePassword?password

### cancelExtractionParameter: 1

*   Parameter 1 - controllerID (long)
*   Call /extraction/cancelExtraction?controllerID
*   Return type boolean

### getArchiveInfoParameter: 2

*   Parameter 1 - linkIds (long\[\])
*     2 - packageIds (long\[\])
*   Call /extraction/getArchiveInfo?linkIds&packageIds
*   Return type List<[ArchiveStatus](#tag_189)\>

### getArchiveSettingsParameter: 1

*   Parameter 1 - archiveIds (String\[\])
*   Call /extraction/getArchiveSettings?archiveIds
*   Return type List<[ArchiveSettings](#tag_191)\>
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### getQueueParameter: 0

*   Call /extraction/getQueue
*   Return type List<[ArchiveStatus](#tag_189)\>

### setArchiveSettingsParameter: 2

*   Parameter 1 - archiveId (String)
*     2 - archiveSettings ([ArchiveSettings](#tag_191))
*   Call /extraction/setArchiveSettings?archiveId&archiveSettings
*   Return type boolean
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### startExtractionNowParameter: 2

*   Parameter 1 - linkIds (long\[\])
*     2 - packageIds (long\[\])
*   Call /extraction/startExtractionNow?linkIds&packageIds
*   Return type Map<String, boolean|null>

### addParameter: 3

*   Parameter 1 - password (String)
*     2 - source (String)
*     3 - url (String)
*   Call /flash/add?password&source&url
*   Possible Error(s) ResponseCode 500 (Internal Server Error); Type:INTERNAL\_SERVER\_ERROR

### addParameter: 4

*   Parameter 1 - String
*     2 - String
*     3 - String
*     4 - String
*   Call /flash/add?String&String&String&String
*   Possible Error(s) ResponseCode 500 (Internal Server Error); Type:INTERNAL\_SERVER\_ERROR

### addParameter: 0

*   Call /flash/add
*   Possible Error(s) ResponseCode 500 (Internal Server Error); Type:INTERNAL\_SERVER\_ERROR

### addcnlParameter: 1

*   Parameter 1 - cnl ([CnlQuery](#tag_200))
*   Call /flash/addcnl?cnl
*   Possible Error(s) ResponseCode 500 (Internal Server Error); Type:INTERNAL\_SERVER\_ERROR

### addcryptedParameter: 0

*   Call /flash/addcrypted
*   Possible Error(s) ResponseCode 500 (Internal Server Error); Type:INTERNAL\_SERVER\_ERROR

### addcrypted2Parameter: 0

*   Call /flash/addcrypted2
*   Possible Error(s) ResponseCode 500 (Internal Server Error); Type:INTERNAL\_SERVER\_ERROR

### addcrypted2RemoteParameter: 3

*   Parameter 1 - crypted (String)
*     2 - jk (String)
*     3 - k (String)
*   Call /flash/addcrypted2Remote?crypted&jk&k

### Parameter: 0

*   Call /flash/
*   Possible Error(s) ResponseCode 500 (Internal Server Error); Type:INTERNAL\_SERVER\_ERROR

### doSomethingCoolParameter: 0

*   Call /jd/doSomethingCool

### getCoreRevisionParameter: 0

*   Call /jd/getCoreRevision
*   Return type Integer

### refreshPluginsParameter: 0

*   Call /jd/refreshPlugins
*   Return type boolean

### sumParameter: 2

*   Parameter 1 - a (int)
*     2 - b (int)
*   Call /jd/sum?a&b
*   Return type int

### uptimeParameter: 0

*   Call /jd/uptime
*   Return type long

### versionParameter: 0

*   Call /jd/version
*   Return type long

### addContainerParameter: 2

*   Parameter 1 - type (String)
*     2 - content (String)
*   Call /linkcollector/addContainer?type&content

### addLinksParameter: 4

*   Parameter 1 - link (String)
*     2 - packageName (String)
*     3 - archivePassword (String)
*     4 - linkPassword (String)
*   Call /linkcollector/addLinks?link&packageName&archivePassword&linkPassword
*   Return type boolean|null

### addLinksParameter: 5

*   Parameter 1 - links (String)
*     2 - packageName (String)
*     3 - extractPassword (String)
*     4 - downloadPassword (String)
*     5 - destinationFolder (String)
*   Call /linkcollector/addLinks?links&packageName&extractPassword&downloadPassword&destinationFolder
*   Return type boolean|null

### addLinksAndStartDownloadParameter: 4

*   Parameter 1 - links (String)
*     2 - packageName (String)
*     3 - extractPassword (String)
*     4 - downloadPassword (String)
*   Call /linkcollector/addLinksAndStartDownload?links&packageName&extractPassword&downloadPassword
*   Return type boolean|null

### disableLinksParameter: 2

*   Parameter 1 - linkIds (List)
*     2 - packageIds (List)
*   Call /linkcollector/disableLinks?linkIds&packageIds
*   Return type boolean

### disableLinksParameter: 1

*   Parameter 1 - linkIds (List)
*   Call /linkcollector/disableLinks?linkIds
*   Return type boolean

### enableLinksParameter: 2

*   Parameter 1 - linkIds (List)
*     2 - packageIds (List)
*   Call /linkcollector/enableLinks?linkIds&packageIds
*   Return type boolean

### enableLinksParameter: 1

*   Parameter 1 - linkIds (List)
*   Call /linkcollector/enableLinks?linkIds
*   Return type boolean

### getChildrenChangedParameter: 1

*   Parameter 1 - structureWatermark (Long)
*   Call /linkcollector/getChildrenChanged?structureWatermark
*   Return type Long

### getDownloadFolderHistorySelectionBaseParameter: 0

*   Call /linkcollector/getDownloadFolderHistorySelectionBase
*   Return type List<String>

### moveLinksParameter: 1

*   Parameter 1 - query ([APIQuery](#tag_18))
*   Call /linkcollector/moveLinks?query
*   Return type boolean

### movePackagesParameter: 1

*   Parameter 1 - query ([APIQuery](#tag_18))
*   Call /linkcollector/movePackages?query
*   Return type boolean

### packageCountParameter: 0

*   Call /linkcollector/packageCount
*   Return type int

### queryLinksParameter: 1

*   Parameter 1 - queryParams ([APIQuery](#tag_18))
*   Call /linkcollector/queryLinks?queryParams
*   Return type List<[CrawledLink](#tag_227)\>

### queryPackagesParameter: 1

*   Parameter 1 - queryParams ([APIQuery](#tag_18))
*   Call /linkcollector/queryPackages?queryParams
*   Return type List<[CrawledPackage](#tag_229)\>

### removeLinksParameter: 2

*   Parameter 1 - packageIds (List)
*     2 - linkIds (List)
*   Call /linkcollector/removeLinks?packageIds&linkIds
*   Return type boolean|null

### removeLinksParameter: 1

*   Parameter 1 - linkIds (List)
*   Call /linkcollector/removeLinks?linkIds
*   Return type boolean|null

### renameLinkParameter: 3

*   Parameter 1 - packageId (Long)
*     2 - linkId (Long)
*     3 - newName (String)
*   Call /linkcollector/renameLink?packageId&linkId&newName
*   Return type boolean

### renamePackageParameter: 2

*   Parameter 1 - packageId (Long)
*     2 - newName (String)
*   Call /linkcollector/renamePackage?packageId&newName
*   Return type boolean

### startDownloadsParameter: 1

*   Parameter 1 - linkIds (List)
*   Call /linkcollector/startDownloads?linkIds
*   Return type boolean|null

### startDownloadsParameter: 2

*   Parameter 1 - linkIds (List)
*     2 - packageIds (List)
*   Call /linkcollector/startDownloads?linkIds&packageIds
*   Return type boolean|null

### isCrawlingParameter: 0

*   Call /linkcrawler/isCrawling
*   Return type boolean

### abortParameter: 0

*   Call /linkgrabberv2/abort
*   Return type boolean

### abortParameter: 1

*   Parameter 1 - jobId (long)
*   Call /linkgrabberv2/abort?jobId
*   Return type boolean

### addContainerParameter: 2

*   Parameter 1 - type (String)
*     2 - content (String)
*   Call /linkgrabberv2/addContainer?type&content
*   Return type [LinkCollectingJob](#tag_242)

### addLinksParameter: 1

*   Parameter 1 - query ([AddLinksQuery](#tag_244))
*   Call /linkgrabberv2/addLinks?query
*   Return type [LinkCollectingJob](#tag_242)

### addVariantCopyParameter: 4

*   Parameter 1 - linkid (long)
*     2 - destinationAfterLinkID (long)
*     3 - destinationPackageID (long)
*     4 - variantID (String)
*   Call /linkgrabberv2/addVariantCopy?linkid&destinationAfterLinkID&destinationPackageID&variantID
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### cleanupParameter: 5

*   Parameter 1 - linkIds (long\[\])
*     2 - packageIds (long\[\])
*     3 - action ([Action](#tag_128))
*     4 - mode ([Mode](#tag_129))
*     5 - selectionType ([SelectionType](#tag_130))
*   Call /linkgrabberv2/cleanup?linkIds&packageIds&action&mode&selectionType
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### clearListParameter: 0

*   Call /linkgrabberv2/clearList
*   Return type boolean

### getChildrenChangedParameter: 1

*   Parameter 1 - structureWatermark (long)
*   Call /linkgrabberv2/getChildrenChanged?structureWatermark
*   Return type long

### getDownloadFolderHistorySelectionBaseParameter: 0

*   Call /linkgrabberv2/getDownloadFolderHistorySelectionBase
*   Return type List<String>

### getDownloadUrlsParameter: 3

*   Parameter 1 - linkIds (long\[\])
*     2 - packageIds (long\[\])
*     3 - urlDisplayTypes (UrlDisplayTypeStorable\[\])
*   Call /linkgrabberv2/getDownloadUrls?linkIds&packageIds&urlDisplayTypes
*   Return type Map<String, List<Long>>
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### getPackageCountParameter: 0

*   Call /linkgrabberv2/getPackageCount
*   Return type int

### getVariantsParameter: 1

*   Parameter 1 - linkid (long)
*   Call /linkgrabberv2/getVariants?linkid
*   Return type List<[LinkVariant](#tag_253)\>
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### isCollectingParameter: 0

*   Call /linkgrabberv2/isCollecting
*   Return type boolean

### moveLinksParameter: 3

*   Parameter 1 - linkIds (long\[\])
*     2 - afterLinkID (long)
*     3 - destPackageID (long)
*   Call /linkgrabberv2/moveLinks?linkIds&afterLinkID&destPackageID
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### movePackagesParameter: 2

*   Parameter 1 - packageIds (long\[\])
*     2 - afterDestPackageId (long)
*   Call /linkgrabberv2/movePackages?packageIds&afterDestPackageId
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### moveToDownloadlistParameter: 2

*   Parameter 1 - linkIds (long\[\])
*     2 - packageIds (long\[\])
*   Call /linkgrabberv2/moveToDownloadlist?linkIds&packageIds
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### movetoNewPackageParameter: 4

*   Parameter 1 - linkIds (long\[\])
*     2 - pkgIds (long\[\])
*     3 - newPkgName (String)
*     4 - downloadPath (String)
*   Call /linkgrabberv2/movetoNewPackage?linkIds&pkgIds&newPkgName&downloadPath
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### queryLinkCrawlerJobsParameter: 1

*   Parameter 1 - query ([LinkCrawlerJobsQuery](#tag_260))
*   Call /linkgrabberv2/queryLinkCrawlerJobs?query
*   Return type List<[JobLinkCrawler](#tag_261)\>

### queryLinksParameter: 1

*   Parameter 1 - queryParams ([CrawledLinkQuery](#tag_265))
*   Call /linkgrabberv2/queryLinks?queryParams
*   Return type List<[CrawledLink](#tag_264)\>
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### queryPackagesParameter: 1

*   Parameter 1 - queryParams ([CrawledPackageQuery](#tag_268))
*   Call /linkgrabberv2/queryPackages?queryParams
*   Return type List<[CrawledPackage](#tag_269)\>
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### removeLinksParameter: 2

*   Parameter 1 - linkIds (long\[\])
*     2 - packageIds (long\[\])
*   Call /linkgrabberv2/removeLinks?linkIds&packageIds
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### renameLinkParameter: 2

*   Parameter 1 - linkId (long)
*     2 - newName (String)
*   Call /linkgrabberv2/renameLink?linkId&newName
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### renamePackageParameter: 2

*   Parameter 1 - packageId (long)
*     2 - newName (String)
*   Call /linkgrabberv2/renamePackage?packageId&newName
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### setDownloadDirectoryParameter: 2

*   Parameter 1 - directory (String)
*     2 - packageIds (long\[\])
*   Call /linkgrabberv2/setDownloadDirectory?directory&packageIds
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### setDownloadPasswordParameter: 3

*   Parameter 1 - linkIds (long\[\])
*     2 - packageIds (long\[\])
*     3 - pass (String)
*   Call /linkgrabberv2/setDownloadPassword?linkIds&packageIds&pass
*   Return type boolean
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### setEnabledParameter: 3

*   Parameter 1 - enabled (boolean)
*     2 - linkIds (long\[\])
*     3 - packageIds (long\[\])
*   Call /linkgrabberv2/setEnabled?enabled&linkIds&packageIds
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### setPriorityParameter: 3

*   Parameter 1 - priority ([Priority](#tag_136))
*     2 - linkIds (long\[\])
*     3 - packageIds (long\[\])
*   Call /linkgrabberv2/setPriority?priority&linkIds&packageIds
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### setVariantParameter: 2

*   Parameter 1 - linkid (long)
*     2 - variantID (String)
*   Call /linkgrabberv2/setVariant?linkid&variantID
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### splitPackageByHosterParameter: 2

*   Parameter 1 - linkIds (long\[\])
*     2 - pkgIds (long\[\])
*   Call /linkgrabberv2/splitPackageByHoster?linkIds&pkgIds

### startOnlineStatusCheckParameter: 2

*   Parameter 1 - linkIds (long\[\])
*     2 - packageIds (long\[\])
*   Call /linkgrabberv2/startOnlineStatusCheck?linkIds&packageIds
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### getAvailableLogsParameter: 0

*   Call /log/getAvailableLogs
*   Return type List<[LogFolder](#tag_282)\>

### sendLogFileParameter: 1

*   Parameter 1 - logFolders (LogFolderStorable\[\])
*   Call /log/sendLogFile?logFolders
*   Return type String
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### getParameter: 3

*   Parameter 1 - interfaceName (String)
*     2 - displayName (String)
*     3 - key (String)
*   Call /plugins/get?interfaceName&displayName&key
*   Return type Object
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### getAllPluginRegexParameter: 0

*   Call /plugins/getAllPluginRegex
*   Return type Map<String, List<String>>

### getPluginRegexParameter: 1

*   Parameter 1 - URL (String)
*   Call /plugins/getPluginRegex?URL
*   Return type List<String>

### listParameter: 1

*   Parameter 1 - query ([PluginsQuery](#tag_289))
*   Call /plugins/list?query
*   Return type List<[Plugin](#tag_290)\>

### queryParameter: 1

*   Parameter 1 - query ([AdvancedConfigQuery](#tag_66))
*   Call /plugins/query?query
*   Return type List<[PluginConfigEntry](#tag_292)\>
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter

### resetParameter: 3

*   Parameter 1 - interfaceName (String)
*     2 - displayName (String)
*     3 - key (String)
*   Call /plugins/reset?interfaceName&displayName&key
*   Return type boolean
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter
*     ResponseCode 400 (Bad Request); Type:INVALID\_VALUE

### setParameter: 4

*   Parameter 1 - interfaceName (String)
*     2 - displayName (String)
*     3 - key (String)
*     4 - newValue (Object)
*   Call /plugins/set?interfaceName&displayName&key&newValue
*   Return type boolean
*   Possible Error(s) ResponseCode 400 (Bad Request); Type:BAD\_PARAMETERS; Description: Bad Parameter
*     ResponseCode 400 (Bad Request); Type:INVALID\_VALUE

### pollParameter: 1

*   Parameter 1 - queryParams ([APIQuery](#tag_18))
*   Call /polling/poll?queryParams
*   Return type List<[PollingResult](#tag_297)\>

### doReconnectParameter: 0

*   Call /reconnect/doReconnect

### disconnectParameter: 0

*   Description invalides the current token
*   Call /session/disconnect
*   Return type boolean

### handshakeParameter: 2

*   Parameter 1 - user (String)
*     2 - password (String)
*   Description returns an un/authenticated token for given username and password or "error" in case login failed
*   Call /session/handshake?user&password
*   Return type String
*   Possible Error(s) ResponseCode 403 (Forbidden); Type:AUTH\_FAILED

### exitJDParameter: 0

*   Call /system/exitJD

### getStorageInfosParameter: 1

*   Parameter 1 - path (String)
*   Call /system/getStorageInfos?path
*   Return type List<StorageInformation>

### getSystemInfosParameter: 0

*   Call /system/getSystemInfos
*   Return type SystemInformation

### hibernateOSParameter: 0

*   Call /system/hibernateOS

### restartJDParameter: 0

*   Call /system/restartJD

### shutdownOSParameter: 1

*   Parameter 1 - force (boolean)
*   Call /system/shutdownOS?force

### standbyOSParameter: 0

*   Call /system/standbyOS

### addLinksFromDOMParameter: 0

*   Call /toolbar/addLinksFromDOM
*   Return type Object

### checkLinksFromDOMParameter: 0

*   Call /toolbar/checkLinksFromDOM
*   Return type Object

### getStatusParameter: 0

*   Call /toolbar/getStatus
*   Return type Object

### isAvailableParameter: 0

*   Call /toolbar/isAvailable
*   Return type boolean

### pollCheckedLinksFromDOMParameter: 1

*   Parameter 1 - checkID (String)
*   Call /toolbar/pollCheckedLinksFromDOM?checkID
*   Return type [LinkCheckResult](#tag_318)

### specialURLHandlingParameter: 1

*   Parameter 1 - url (String)
*   Call /toolbar/specialURLHandling?url
*   Return type String

### startDownloadsParameter: 0

*   Call /toolbar/startDownloads
*   Return type boolean

### stopDownloadsParameter: 0

*   Call /toolbar/stopDownloads
*   Return type boolean

### toggleAutomaticReconnectParameter: 0

*   Call /toolbar/toggleAutomaticReconnect
*   Return type boolean

### toggleClipboardMonitoringParameter: 0

*   Call /toolbar/toggleClipboardMonitoring
*   Return type boolean

### toggleDownloadSpeedLimitParameter: 0

*   Call /toolbar/toggleDownloadSpeedLimit
*   Return type boolean

### togglePauseDownloadsParameter: 0

*   Call /toolbar/togglePauseDownloads
*   Return type boolean

### togglePremiumParameter: 0

*   Call /toolbar/togglePremium
*   Return type boolean

### toggleStopAfterCurrentDownloadParameter: 0

*   Call /toolbar/toggleStopAfterCurrentDownload
*   Return type boolean

### triggerUpdateParameter: 0

*   Call /toolbar/triggerUpdate
*   Return type boolean

### getMenuParameter: 1

*   Parameter 1 - context ([Context](#tag_332))
*   Description Get the custom menu structure for the desired context
*   Call /ui/getMenu?context
*   Return type [MyJDMenuItem](#tag_334)
*   Possible Error(s) ResponseCode 500 (Internal Server Error); Type:INTERNAL\_SERVER\_ERROR
*     ResponseCode 404 (Not Found); Type:Not Found

### invokeActionParameter: 4

*   Parameter 1 - context ([Context](#tag_332))
*     2 - id (String)
*     3 - linkIds (long\[\])
*     4 - packageIds (long\[\])
*   Description Invoke a menu action on our selection and get the results.
*   Call /ui/invokeAction?context&id&linkIds&packageIds
*   Return type Object
*   Possible Error(s) ResponseCode 500 (Internal Server Error); Type:INTERNAL\_SERVER\_ERROR
*     ResponseCode 404 (Not Found); Type:Not Found

### isUpdateAvailableParameter: 0

*   Call /update/isUpdateAvailable
*   Return type boolean

### restartAndUpdateParameter: 0

*   Call /update/restartAndUpdate

### runUpdateCheckParameter: 0

*   Call /update/runUpdateCheck

*   BOOLEAN
*   INT
*   LONG
*   STRING
*   OBJECT
*   OBJECT\_LIST
*   STRING\_LIST
*   ENUM
*   BYTE
*   CHAR
*   DOUBLE
*   FLOAT
*   SHORT
*   BOOLEAN\_LIST
*   BYTE\_LIST
*   SHORT\_LIST
*   LONG\_LIST
*   INT\_LIST
*   FLOAT\_LIST
*   ENUM\_LIST
*   DOUBLE\_LIST
*   CHAR\_LIST
*   UNKNOWN
*   HEX\_COLOR
*   HEX\_COLOR\_LIST
*   ACTION

*   DELETE\_ALL
*   DELETE\_DISABLED
*   DELETE\_FAILED
*   DELETE\_FINISHED
*   DELETE\_OFFLINE
*   DELETE\_DUPE
*   DELETE\_MODE

*   COMPLETEFile is available  for extraction
*   INCOMPLETEFile exists, but is incomplete
*   MISSINGFile does not exist

*   ONLINE
*   OFFLINE
*   UNKNOWN
*   TEMP\_UNKNOWN

*   LGCLinkgrabber Rightclick Context
*   DLCDownloadlist Rightclick Context

*   RUNNINGExtraction is currently running
*   QUEUEDArchive is queued for extraction and will run as soon as possible
*   NANo controller assigned

*   REMOVE\_LINKS\_AND\_DELETE\_FILES
*   REMOVE\_LINKS\_AND\_RECYCLE\_FILES
*   REMOVE\_LINKS\_ONLY

*   HIGHEST
*   HIGHER
*   HIGH
*   DEFAULT
*   LOW
*   LOWER
*   LOWEST

*   CONNECTION\_UNAVAILABLE
*   TOO\_MANY\_RETRIES
*   CAPTCHA
*   MANUAL
*   DISK\_FULL
*   NO\_ACCOUNT
*   INVALID\_DESTINATION
*   FILE\_EXISTS
*   UPDATE\_RESTART\_REQUIRED
*   FFMPEG\_MISSING
*   FFPROBE\_MISSING

*   NA
*   PENDING
*   FINISHED

*   SELECTED
*   UNSELECTED
*   ALL
*   NONE

*   SINGLE
*   BLOCK\_HOSTER
*   BLOCK\_ALL\_CAPTCHAS
*   BLOCK\_PACKAGE
*   REFRESH
*   STOP\_CURRENT\_ACTION
*   TIMEOUT

*   CONTAINER
*   ACTION
*   LINK

*   FTP
*   HTTP

```

          myAPIQuery = 
                  {
                    "empty"      = (boolean),
                    "forNullKey" = (V),
                    "maxResults" = (Integer),
                    "startAt"    = (Integer)
                  }
```


```

          myAccount = 
                  {
                    "enabled"     = (boolean),
                    "errorString" = (String),
                    "errorType"   = (String),
                    "hostname"    = (String),
                    "trafficLeft" = (Long),
                    "trafficMax"  = (Long),
                    "username"    = (String),
                    "uuid"        = (Long),
                    "valid"       = (boolean),
                    "validUntil"  = (Long)
                  }
```


```

          myAccount = 
                  {
                    "hostname" = (String),
                    "infoMap"  = (JsonMap),
                    "uuid"     = (long)
                  }
```


```

          myAccountQuery = 
                  {
                    "enabled"     = (boolean),
                    "error"       = (boolean),
                    "maxResults"  = (int),
                    "startAt"     = (int),
                    "trafficLeft" = (boolean),
                    "trafficMax"  = (boolean),
                    "userName"    = (boolean),
                    "uuidlist"    = (UnorderedList<Long>),
                    "valid"       = (boolean),
                    "validUntil"  = (boolean)
                  }
```


```

          myAddLinksQuery = 
                  {
                    "assignJobID"              = (boolean|null),
                    "autoExtract"              = (boolean|null),
                    "autostart"                = (boolean|null),
                    "dataURLs"                 = (String[]),
                    "deepDecrypt"              = (boolean|null),
                    "destinationFolder"        = (String),
                    "downloadPassword"         = (String),
                    "extractPassword"          = (String),
                    "links"                    = (String),
                    "overwritePackagizerRules" = (boolean|null),
                    "packageName"              = (String),
                    "priority"                 = (Priority),
                    "sourceUrl"                = (String)
                  }
```


```

          myAdvancedConfigAPIEntry = 
                  {
                    "abstractType"  = (AbstractType),
                    "defaultValue"  = (Object),
                    "docs"          = (String),
                    "enumLabel"     = (String),
                    "enumOptions"   = (String[][]),
                    "interfaceName" = (String),
                    "key"           = (String),
                    "storage"       = (String),
                    "type"          = (String),
                    "value"         = (Object)
                  }
```


```

          myAdvancedConfigQuery = 
                  {
                    "configInterface"   = (String),
                    "defaultValues"     = (boolean),
                    "description"       = (boolean),
                    "enumInfo"          = (boolean),
                    "includeExtensions" = (boolean),
                    "pattern"           = (String),
                    "values"            = (boolean)
                  }
```


```


    /**
     * Leave Boolean values empty or set them to null if you want JD to use the default value
     */
          myArchiveSettings = 
                  {
                    "archiveId"                          = (String),
                    "autoExtract"                        = (boolean|null),
                    "extractPath"                        = (String),
                    "finalPassword"                      = (String),
                    "passwords"                          = (List<String>),
                    "removeDownloadLinksAfterExtraction" = (boolean|null),
                    "removeFilesAfterExtraction"         = (boolean|null)
                  }
```


```

          myArchiveStatus = 
                  {
    /**
     * ID to adress the archive. Used for example for extraction/getArchiveSettings?[,,...]
     */

                    "archiveId"        = (String)ID to adress the archive. Used for example for extraction/getArchiveSettings?[,,...],
                    "archiveName"      = (String),
    /**
     * -1 or the controller ID if any controller is active. Used in cancelExtraction? 
     */

                    "controllerId"     = (long)-1 or the controller ID if any controller is active. Used in cancelExtraction? ,
    /**
     * The status of the controller
     */

                    "controllerStatus" = (ControllerStatus)The status of the controller,
    /**
     * Map Keys: Filename of the Part-File. Values: ArchiveFileStatus
     * Example: 
     * {
     * "archive.part1.rar":"COMPLETE",
     * "archive.part2.rar":"MISSING"
     * }
     */

                    "states"           = (Map<String, ArchiveFileStatus>)Map Keys: Filename of the Part-File. Values: ArchiveFileStatus
Example: 
{
"archive.part1.rar":"COMPLETE",
"archive.part2.rar":"MISSING"
},
    /**
     * Type of the archive. e.g. "GZIP_SINGLE", "RAR_MULTI","RAR_SINGLE",.... 
     */

                    "type"             = (String)Type of the archive. e.g. "GZIP_SINGLE", "RAR_MULTI","RAR_SINGLE",.... 
                  }
```


```

          myBasicAuthentication = 
                  {
                    "created"       = (long),
                    "enabled"       = (boolean),
                    "hostmask"      = (String),
                    "id"            = (long),
                    "lastValidated" = (long),
                    "password"      = (String),
                    "type"          = (Type),
                    "username"      = (String)
                  }
```


```

          myCaptchaJob = 
                  {
                    "captchaCategory" = (String),
                    "created"         = (long),
                    "explain"         = (String),
                    "hoster"          = (String),
                    "id"              = (long),
                    "link"            = (long),
                    "timeout"         = (int),
                    "type"            = (String)
                  }
```


```

          myCnlQuery = 
                  {
                    "comment"     = (String),
                    "crypted"     = (String),
                    "dir"         = (String),
                    "jk"          = (String),
                    "key"         = (String),
                    "orgReferrer" = (String),
                    "orgSource"   = (String),
                    "packageName" = (String),
                    "passwords"   = (List<String>),
                    "permission"  = (boolean),
                    "referrer"    = (String),
                    "source"      = (String),
                    "urls"        = (String)
                  }
```


```

          myCrawledLink = 
                  {
                    "availability"     = (AvailableLinkState),
                    "bytesTotal"       = (long),
                    "comment"          = (String),
                    "downloadPassword" = (String),
                    "enabled"          = (boolean),
                    "host"             = (String),
                    "name"             = (String),
                    "packageUUID"      = (long),
                    "priority"         = (Priority),
                    "url"              = (String),
                    "uuid"             = (long),
                    "variant"          = (LinkVariant),
                    "variants"         = (boolean)
                  }
```


```

          myCrawledLink = 
                  {
                    "infoMap"  = (JsonMap),
                    "name"     = (String),
                    "uniqueID" = (Long),
                    "uuid"     = (Long)
                  }
```


```

          myCrawledLinkQuery = 
                  {
                    "availability" = (boolean),
                    "bytesTotal"   = (boolean),
                    "comment"      = (boolean),
                    "enabled"      = (boolean),
                    "host"         = (boolean),
                    "jobUUIDs"     = (long[]),
                    "maxResults"   = (int),
                    "packageUUIDs" = (long[]),
                    "password"     = (boolean),
                    "priority"     = (boolean),
                    "startAt"      = (int),
                    "status"       = (boolean),
                    "url"          = (boolean),
                    "variantID"    = (boolean),
                    "variantIcon"  = (boolean),
                    "variantName"  = (boolean),
                    "variants"     = (boolean)
                  }
```


```

          myCrawledPackage = 
                  {
                    "infoMap" = (JsonMap),
                    "name"    = (String),
                    "uuid"    = (long)
                  }
```


```

          myCrawledPackage = 
                  {
                    "bytesTotal"       = (long),
                    "childCount"       = (int),
                    "comment"          = (String),
                    "downloadPassword" = (String),
                    "enabled"          = (boolean),
                    "hosts"            = (String[]),
                    "name"             = (String),
                    "offlineCount"     = (int),
                    "onlineCount"      = (int),
                    "priority"         = (Priority),
                    "saveTo"           = (String),
                    "tempUnknownCount" = (int),
                    "unknownCount"     = (int),
                    "uuid"             = (long)
                  }
```


```

          myCrawledPackageQuery = 
                  {
                    "availableOfflineCount"     = (boolean),
                    "availableOnlineCount"      = (boolean),
                    "availableTempUnknownCount" = (boolean),
                    "availableUnknownCount"     = (boolean),
                    "bytesTotal"                = (boolean),
                    "childCount"                = (boolean),
                    "comment"                   = (boolean),
                    "enabled"                   = (boolean),
                    "hosts"                     = (boolean),
                    "maxResults"                = (int),
                    "packageUUIDs"              = (long[]),
                    "priority"                  = (boolean),
                    "saveTo"                    = (boolean),
                    "startAt"                   = (int),
                    "status"                    = (boolean)
                  }
```


```

          myDialogInfo = 
                  {
                    "properties" = (Map<String, String>),
                    "type"       = (String)
                  }
```


```

          myDialogTypeInfo = 
                  {
                    "in"  = (Map<String, String>),
                    "out" = (Map<String, String>)
                  }
```


```

          myDownloadLink = 
                  {
                    "infoMap" = (JsonMap),
                    "name"    = (String),
                    "uuid"    = (long)
                  }
```


```

          myDownloadLink = 
                  {
                    "addedDate"        = (long),
                    "bytesLoaded"      = (long),
                    "bytesTotal"       = (long),
                    "comment"          = (String),
                    "downloadPassword" = (String),
                    "enabled"          = (boolean),
                    "eta"              = (long),
                    "extractionStatus" = (String),
                    "finished"         = (boolean),
                    "finishedDate"     = (long),
                    "host"             = (String),
                    "name"             = (String),
                    "packageUUID"      = (long),
                    "priority"         = (Priority),
                    "running"          = (boolean),
                    "skipped"          = (boolean),
                    "speed"            = (long),
                    "status"           = (String),
                    "statusIconKey"    = (String),
                    "url"              = (String),
                    "uuid"             = (long)
                  }
```


```

          myDownloadListDiff = 
                  {
                  }
```


```

          myEnumOption = 
                  {
                    "label" = (String),
                    "name"  = (String)
                  }
```


```

          myExtension = 
                  {
                    "configInterface" = (String),
                    "description"     = (String),
                    "enabled"         = (boolean),
                    "iconKey"         = (String),
                    "id"              = (String),
                    "installed"       = (boolean),
                    "name"            = (String)
                  }
```


```

          myExtensionQuery = 
                  {
                    "configInterface" = (boolean),
                    "description"     = (boolean),
                    "enabled"         = (boolean),
                    "iconKey"         = (boolean),
                    "installed"       = (boolean),
                    "name"            = (boolean),
                    "pattern"         = (String)
                  }
```


```

          myFilePackage = 
                  {
                    "infoMap" = (JsonMap),
                    "name"    = (String),
                    "uuid"    = (long)
                  }
```


```

          myFilePackage = 
                  {
                    "activeTask"       = (String),
                    "bytesLoaded"      = (long),
                    "bytesTotal"       = (long),
                    "childCount"       = (int),
                    "comment"          = (String),
                    "downloadPassword" = (String),
                    "enabled"          = (boolean),
                    "eta"              = (long),
                    "finished"         = (boolean),
                    "hosts"            = (String[]),
                    "name"             = (String),
                    "priority"         = (Priority),
                    "running"          = (boolean),
                    "saveTo"           = (String),
                    "speed"            = (long),
                    "status"           = (String),
                    "statusIconKey"    = (String),
                    "uuid"             = (long)
                  }
```


```

          myIconDescriptor = 
                  {
                    "cls"  = (String),
                    "key"  = (String),
                    "prps" = (Map<String, Object>),
                    "rsc"  = (List<IconDescriptor>)
                  }
```


```

          myJobLinkCrawler = 
                  {
                    "broken"    = (int),
                    "checking"  = (boolean),
                    "crawled"   = (int),
                    "crawlerId" = (long),
                    "crawling"  = (boolean),
                    "filtered"  = (int),
                    "jobId"     = (long),
                    "unhandled" = (int)
                  }
```


```

          myLinkCheckResult = 
                  {
                    "links"  = (List<LinkStatus>),
                    "status" = (STATUS)
                  }
```


```

          myLinkCollectingJob = 
                  {
                    "id" = (long)
                  }
```


```

          myLinkCrawlerJobsQuery = 
                  {
                    "collectorInfo" = (boolean),
                    "jobIds"        = (long[])
                  }
```


```

          myLinkQuery = 
                  {
                    "addedDate"        = (boolean),
                    "bytesLoaded"      = (boolean),
                    "bytesTotal"       = (boolean),
                    "comment"          = (boolean),
                    "enabled"          = (boolean),
                    "eta"              = (boolean),
                    "extractionStatus" = (boolean),
                    "finished"         = (boolean),
                    "finishedDate"     = (boolean),
                    "host"             = (boolean),
                    "jobUUIDs"         = (long[]),
                    "maxResults"       = (int),
                    "packageUUIDs"     = (long[]),
                    "password"         = (boolean),
                    "priority"         = (boolean),
                    "running"          = (boolean),
                    "skipped"          = (boolean),
                    "speed"            = (boolean),
                    "startAt"          = (int),
                    "status"           = (boolean),
                    "url"              = (boolean)
                  }
```


```

          myLinkStatus = 
                  {
                    "host"        = (String),
                    "linkCheckID" = (String),
                    "name"        = (String),
                    "size"        = (long),
                    "status"      = (AvailableLinkState),
                    "url"         = (String)
                  }
```


```

          myLinkVariant = 
                  {
                    "iconKey" = (String),
                    "id"      = (String),
                    "name"    = (String)
                  }
```


```

          myLinkVariant = 
                  {
                    "iconKey" = (String),
                    "id"      = (String),
                    "name"    = (String)
                  }
```


```

          myLogFolder = 
                  {
                    "created"      = (long),
                    "current"      = (boolean),
                    "lastModified" = (long)
                  }
```


```

          myMenuStructure = 
                  {
                    "children" = (List<MenuStructure>),
                    "icon"     = (String),
                    "id"       = (String),
                    "name"     = (String),
                    "type"     = (Type)
                  }
```


```

          myMyJDMenuItem = 
                  {
                    "children" = (List<MenuStructure>),
                    "icon"     = (String),
                    "id"       = (String),
                    "name"     = (String),
                    "type"     = (Type)
                  }
```


```

          myPackageQuery = 
                  {
                    "bytesLoaded"  = (boolean),
                    "bytesTotal"   = (boolean),
                    "childCount"   = (boolean),
                    "comment"      = (boolean),
                    "enabled"      = (boolean),
                    "eta"          = (boolean),
                    "finished"     = (boolean),
                    "hosts"        = (boolean),
                    "maxResults"   = (int),
                    "packageUUIDs" = (long[]),
                    "priority"     = (boolean),
                    "running"      = (boolean),
                    "saveTo"       = (boolean),
                    "speed"        = (boolean),
                    "startAt"      = (int),
                    "status"       = (boolean)
                  }
```


```

          myPlugin = 
                  {
                    "abstractType"  = (AbstractType),
                    "className"     = (String),
                    "defaultValue"  = (Object),
                    "displayName"   = (String),
                    "docs"          = (String),
                    "enumLabel"     = (String),
                    "enumOptions"   = (String[][]),
                    "interfaceName" = (String),
                    "key"           = (String),
                    "pattern"       = (String),
                    "storage"       = (String),
                    "type"          = (String),
                    "value"         = (Object),
                    "version"       = (String)
                  }
```


```

          myPluginConfigEntry = 
                  {
                    "abstractType"  = (AbstractType),
                    "defaultValue"  = (Object),
                    "docs"          = (String),
                    "enumLabel"     = (String),
                    "enumOptions"   = (String[][]),
                    "interfaceName" = (String),
                    "key"           = (String),
                    "storage"       = (String),
                    "type"          = (String),
                    "value"         = (Object)
                  }
```


```

          myPluginsQuery = 
                  {
                    "pattern" = (boolean),
                    "version" = (boolean)
                  }
```


```

          myPollingResult = 
                  {
                    "eventData" = (JsonMap),
                    "eventName" = (String)
                  }
```


```

          myPublisherResponse = 
                  {
                    "eventids"  = (String[]),
                    "publisher" = (String)
                  }
```


```

          mySubscriptionResponse = 
                  {
                    "exclusions"     = (String[]),
                    "maxKeepalive"   = (long),
                    "maxPolltimeout" = (long),
                    "subscribed"     = (boolean),
                    "subscriptionid" = (long),
                    "subscriptions"  = (String[])
                  }
```


```

          mySubscriptionStatusResponse = 
                  {
                    "queueSize"      = (int),
                    "subscribed"     = (boolean),
                    "subscriptionid" = (long)
                  }
```
