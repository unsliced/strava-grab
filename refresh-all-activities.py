import pandas as pd
import requests
import json
import time

# based on https://medium.com/swlh/using-python-to-connect-to-stravas-api-and-analyse-your-activities-dummies-guide-5f49727aac86 

## Get the tokens from file to connect to Strava
with open('strava_tokens.json') as json_file:
    strava_tokens = json.load(json_file)## If access_token has expired then use the refresh_token to get the new access_token

if strava_tokens['expires_at'] < time.time():#Make Strava auth API call with current refresh token


    with open("creds.json") as creds_file: 
        creds = json.load(creds_file)

    client_id = creds["Client ID"]
    client_secret = creds["Client Secret"]

    response = requests.post(
                        url = 'https://www.strava.com/oauth/token',
                        data = {
                                'client_id': client_id,
                                'client_secret': client_secret,
                                'grant_type': 'refresh_token',
                                'refresh_token': strava_tokens['refresh_token']
                                }
                    )#Save response as json in new variable
    new_strava_tokens = response.json()# Save new tokens to file
    with open('strava_tokens.json', 'w') as outfile:
        json.dump(new_strava_tokens, outfile)
        #Use new Strava tokens from now
    strava_tokens = new_strava_tokens
    #Loop through all activities

page = 1
url = "https://www.strava.com/api/v3/activities"
access_token = strava_tokens['access_token']## Create the dataframe ready for the API call to store your activity data
cols = [
            "id",
            "name",
            "start_date_local",
            "type",
            "distance",
            "moving_time",
            "elapsed_time",
            "average_heartrate",
            "gear_id",
            "total_elevation_gain",
            "end_latlng",
            "external_id"
    ]


activities = pd.DataFrame(
    columns = cols 
    
)

while True:    
    # get page of activities from Strava
    r = requests.get(url + '?access_token=' + access_token + '&per_page=200' + '&page=' + str(page))
    r = r.json()# if no results then exit loop
    if (not r):
        break
    
    # otherwise add new data to dataframe
    for x in range(len(r)):
        for c in cols: 
            activities.loc[x + (page-1)*200, c] = r[x][c]
        # increment page
    page += 1
    
activities.to_csv('strava_activities.csv')