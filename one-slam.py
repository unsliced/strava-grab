import pandas as pd
import requests
import json
import time
import arrow 

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

f = 'YYYY-MM-DD'
onefeb = arrow.get("2021-02-01")
# conversion factor
conv_fac = 0.621371
totalseconds = 0
totalkm = 0

# get page of activities from Strava
r = requests.get(url + '?access_token=' + access_token + '&per_page=200' + '&page=' + str(page)+ '&after=' + str(onefeb.shift(days=-1).timestamp))
r = r.json()# if no results then exit loop
if (not r):
    pass
else: 
    # otherwise add new data to dataframe
    for row in r:

        if(row['type'] != "Run"):
            continue 


        f = 'YYYY-MM-DD'

        # e.g. 2021-02-14T14:12:57Z 
        dt = arrow.get(row['start_date_local'][:10], f)
        
        # ignore anything before the first of feb 
        if dt < onefeb: break 

        dt = dt.format('YYYY-MM-DD')
        km = row['distance'] / 1000.0
        totalkm += km
        mi = km * conv_fac

        secs = row['elapsed_time'] 
        totalseconds += secs 
        secs = arrow.get(2020, 1, 1, 0, 0, 0).shift(seconds=secs)

        secs = secs.format("H:mm:ss")

        print(f"{dt}: {km:00.2f}km/{mi:00.2f}mi {secs}s [https://www.strava.com/activities/{row['id']}]")


secs = arrow.get(2020, 1, 1, 0, 0, 0).shift(seconds=totalseconds)
days = int(secs.format("DD")) -1
days = str(days)+":" if days > 0 else ""


secs = secs.format("HH:mm:ss")

diff = (arrow.now()-onefeb)
prop = diff/(arrow.get("2021-04-01")-onefeb)

target = prop * 400 / conv_fac
delta = target - totalkm 
ab = "ahead" if delta < 0 else "behind"
delta = abs(delta)

approxd = diff.days * (400 /conv_fac)/totalkm
approxs = diff.seconds * (400 /conv_fac)/totalkm


endtime = onefeb.shift(days=approxd)
endtime = endtime.shift(seconds=approxs)
endtime = endtime.format("YYYY-MM-DD HH:mm")

print(f"\nTotals: {totalkm:.2f}km, {days}{secs}\nFinishing: {endtime}\n{ab} {delta:0.2f}km")
