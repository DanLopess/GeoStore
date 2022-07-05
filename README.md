## Design and Implementation of Distributed Applications Project

### Run client and server individually

To run the project we must first start two servers, via input, with id 2 and id 3.
Because the PM doesn't send the network mapping yet, we create this directly on the client and server side, so the tests and available inputs are limited to what the mapping has.

To run the client, we need to build the project first and then copy the "scripts" directory to /bin/debug/netcoreapp3.1 and change the file name on the Client.cs, according to the script we want to test.

### Run Puppet Master and PCS

To run Puppet Master and PCS open the PCS's executable first and then the Puppet Master's.
Communication between PM and PCS is not working, however, all of the available commands can be read on PM

### Authors
**Group G27**
| Number | Name              |
| -----------|----------------|
| 90590  | Daniel Lopes     |
| 90593  | Diogo Barata      |
| 90598  | Filipe Rodrigues  |
