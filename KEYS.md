# Keys

Keys are required for decrypting most of the file formats used by the Nintendo Switch.

Keysets are stored as text files, and are loaded from `$HOME/.switch`. These 3 filenames are automatically read:  
`prod.keys` - Contains common keys usedy by all Switch devices.  
`console.keys` - Contains console-unique keys.  
`title.keys` - Contains game-specific keys.

#### XTS-AES keys note

The Switch uses 128-bit XTS-AES for decrypting the built-in storage (BIS), NCA header and the SD card contents.
This encryption method uses 2 128-bit keys: a "data" or "cipher" key, and a "tweak" key.

In the keyfile these are stored as one 256-bit key with the data key first, followed by the tweak key.

## Keyfile format

`prod.keys` and `console.keys` should be in the following format with one key per line:  
`key_name = hexadecimal_key_value`

e.g. (Not actual keys)
```
master_key_00     = 63C9FCB338CDE3D037D29BB66F897C6B
master_key_01     = 4636CB976DFE95095C1F55151A8326C6
header_key_source = 343795270AAD5D19EBE2956C9BC71F4C41836B21DC6ACD7BACD4F6AF4816692C
```

#### Title Keys

`title.keys` should be in the following format with one key per line:  
`rights_id,hexadecimal_key_value`.

e.g. (Not actual keys)
```
01000000000100000000000000000003,B4A1F5575D7D8A81624ED36D4E4BD8FD
01000000000108000000000000000003,C8AD76F8C78E241ADFEE6EB12E33F1BD
01000000000108000000000000000004,F9C8EAD30BB594434E4AF62C483CD796
```

## Keyfile templates

This template contains the keys needed to derive all the keys used by hactoolnet, although not all of them are needed for every task.

Fill out the template with the actual keys to get a working keyfile.

```
master_key_source                = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
master_key_00                    = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
master_key_01                    = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
master_key_02                    = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
master_key_03                    = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
master_key_04                    = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
master_key_05                    = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX

keyblob_mac_key_source           = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
keyblob_key_source_00            = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
keyblob_key_source_01            = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
keyblob_key_source_02            = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
keyblob_key_source_03            = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
keyblob_key_source_04            = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
keyblob_key_source_05            = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX

package1_key_00                  = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
package1_key_01                  = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
package1_key_02                  = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
package1_key_03                  = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
package1_key_04                  = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX

package2_key_source              = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX

aes_kek_generation_source        = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
aes_key_generation_source        = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
titlekek_source                  = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX

key_area_key_application_source  = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
key_area_key_ocean_source        = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
key_area_key_system_source       = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX

sd_card_kek_source               = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
sd_card_save_key_source          = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
sd_card_nca_key_source           = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX

header_kek_source                = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
header_key_source                = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX

xci_header_key                   = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX

retail_specific_aes_key_source   = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
per_console_key_source           = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
eticket_rsa_kek                  = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX

bis_key_source_00                = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
bis_key_source_01                = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
bis_key_source_02                = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
bis_kek_source                   = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX

save_mac_kek_source              = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
save_mac_key_source              = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
```

### Console-unique keys

This template is for console-unique keys.

```
tsec_key         = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
secure_boot_key  = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
sd_seed          = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX

# The below keys can be derived from tsec_key and secure_boot_key
device_key       = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
bis_key_00       = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
bis_key_01       = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
bis_key_02       = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
bis_key_03       = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
```

## Complete key list
Below is a complete list of keys that are currently recognized.  
\## represents a hexadecimal number between 00 and 1F  

### Common keys

```
master_key_source
keyblob_mac_key_source
package2_key_source
aes_kek_generation_source
aes_key_generation_source
key_area_key_application_source
key_area_key_ocean_source
key_area_key_system_source
titlekek_source
header_kek_source
header_key_source
sd_card_kek_source
sd_card_nca_key_source
sd_card_save_key_source
retail_specific_aes_key_source
per_console_key_source
bis_kek_source
bis_key_source_00
bis_key_source_01
bis_key_source_02
save_mac_kek_source
save_mac_key_source

header_key
xci_header_key
eticket_rsa_kek

master_key_##
package1_key_##
package2_key_##
titlekek_##
key_area_key_application_##
key_area_key_ocean_##
key_area_key_system_##
keyblob_key_source_##
keyblob_##
```

### Console-unique keys

```
secure_boot_key
tsec_key
device_key
bis_key_00
bis_key_01
bis_key_02
bis_key_03

keyblob_key_##
keyblob_mac_key_##
encrypted_keyblob_##

sd_seed
```
