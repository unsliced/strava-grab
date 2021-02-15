import requests
import json

# Make Strava auth API call with your 
# client_code, client_secret and code

with open("creds.json") as creds_file: 
    creds = json.load(creds_file)

client_id = creds["Client ID"]
client_secret = creds["Client Secret"]
client_code = creds["Client Code"] #n.b. the code is a one shot that will need to be refreshed, cannot be reused 

print(client_id)
print(client_secret)
print(client_code)

response = requests.post(
                    url = 'https://www.strava.com/oauth/token',
                    data = {
                            'client_id': client_id,
                            'client_secret': client_secret,
                            'code': client_code,
                            'grant_type': 'authorization_code'
                            }
                )#Save json response as a variable
strava_tokens = response.json()# Save tokens to file
with open('strava_tokens.json', 'w') as outfile:
    json.dump(strava_tokens, outfile)# Open JSON file and print the file contents 
# to check it's worked properly
with open('strava_tokens.json') as check:
  data = json.load(check)
print(data)

