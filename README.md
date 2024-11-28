# Domain Block List
CLI that can be used to compile a list of ad domains as a block list for Bind9, hosts file, Pi-hole, etc.

## Build the solution
```
dotnet build --configuration release
```

## Publish a single, self-contained executable for Linux-x64 runtime
```
dotnet publish /p:PublishSingleFile=true --configuration release --sc true --runtime linux-x64
```

## Usage
Read the manual by providing the --help parameter:
```
DomainBlockList --help
```
```
Description:
  Domain block list generator for Bind9, hosts file, Pi-hole, etc.

Usage:
  DomainBlockList [options]

Options:
  --output <output>            The output file, containing all formatted domains. [default: /home/user/DomainBlockList/named.conf.blocks]
  --type <Bind9|Custom|Hosts>  Type of the format used for each line. Possible values are Bind9, Hosts, and Custom (--format can be provided to specify the format). [default: Bind9]
  --format <format>            Custom string format of each line. The format must include the {0} placeholder for the domain name. [default: {0}]
  --version                    Show version information
  -?, -h, --help               Show help and usage information
  ```

### Generating a hosts file
```
DomainBlockList --type hosts --output /home/user/hosts
```

### Generating a Bind9 configuration file
```
DomainBlockList --output /home/user/hosts
```

This will compile a Bind9 by default configuration file, defining each domain as a separate zone with 1 configuration file. 

The line format will be `zone "{0}" { type primary; file "/etc/bind/zones/db.blocks"; };`

Once the file is compiled you need to add it to your Bind9 configuration. By default that is located in `/etc/bind/named.conf`

Here is a sample of how the zone file could look like (`/etc/bind/zones/db.blocks`):
```
; Zone file for blocked domains
$TTL    604800
@       IN      SOA     ns1.local.network. admin.local.network. (
                            123         ; Serial
                         604800         ; Refresh
                          86400         ; Retry
                        2419200         ; Expire
                         604800 )       ; Negative Cache TTL

        IN      NS      ns1
; Point the main record and all sub-domains to an internal IP 
; that either blocks or returns a successful status code to all requests
@               IN      A       127.0.0.1
*               IN      A       127.0.0.1
```

### Generating a custom file
Use the `custom` type parameter and specify the desired format used the `--format` parameter. The format parameter must contain the `{0}` placeholder where the domain will be inserted.
```
DomainBlockList --type custom --format "192.168.1.111 {0}" --output /home/user/custom
```