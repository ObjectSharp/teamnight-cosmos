### Description

This project was built to learn about scaling an application in Azure built 
with app services and cosomos db.

You can run it locally using the cosmos db emulator : 
https://docs.microsoft.com/en-us/azure/cosmos-db/local-emulator

To run it in the cloud you should spin up 3 app services, one in East, Central, and West 
(or whereever you want).  You will need to go into the code and change the urls.

You can host the client in azure as well, but runs just fine locally.

Once you setup cosmos db you will need to replicate it out into various regions and play 
around with the consistency and RU/s values.


