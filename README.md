# strava-grab

Based at least in part on ideas [found here](https://medium.com/swlh/using-python-to-connect-to-stravas-api-and-analyse-your-activities-dummies-guide-5f49727aac86).

**NB** the credentials are stored in json file, `creds.json`, which is not on the repo (obvs) but is of the form:  

```json
{
	"Client ID": 00000, 
	"Client Secret": "guid",
}
```

and then session keys are stored in another tokens file (see `initial-poc.py`).
