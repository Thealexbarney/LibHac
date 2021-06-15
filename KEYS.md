# Keys

Keys are required for decrypting most of the file formats used by the Nintendo Switch.

Key sets are stored as text files, and are loaded from `$HOME/.switch`. On Windows this path is usually `C:\Users\<your_username>\.switch`.

These 4 filenames are automatically read:  
`prod.keys` - Contains keys shared by all retail Switch devices.  
`dev.keys` - Contains keys shared by all development Switch devices. Optional.  
`console.keys` - Contains console-unique keys. Optional.  
`title.keys` - Contains game-specific keys.

## Obtaining keys

Keys can be obtained from a Switch that can run homebrew. The easiest way is to use [Lockpick_RCM](https://github.com/shchmue/Lockpick_RCM). See an up-to-date Switch homebrew guide for details.

After running Lockpick_RCM `/switch/prod.keys` and `/switch/title.keys` should be on your SD card. Copy these two files to the `.switch` directory specified above.

# Key file details
Dumping keys from a Switch is all that is needed for LibHac.

The following section contains some additional information on keys, documentation on the key file format and a list of supported keys.

## Key file format

`prod.keys`, `dev.keys` and `console.keys` should be in the following format with one key per line:  
`key_name = hexadecimal_key_value`

Each line must contain fewer than 1024 characters.

e.g. (Not actual keys)
```
master_key_00     = 496620796F752772652072656164696E
master_key_01     = 6720746869732C20796F752772652061
header_key_source = 206E657264AD5D19EBE2956C9BC71F4C41836B21DC6ACD7BACD4F6AF4816692C
```

### Title keys

`title.keys` should be in the following format with one key per line:  
`rights_id = hexadecimal_key_value`.

e.g. (Not actual keys)
```
01000000000100000000000000000003 = 68747470733A2F2F7777772E796F7574
01000000000108000000000000000003 = 7562652E636F6D2F77617463683F763D
01000000000108000000000000000004 = 64517734773957675863513F4C696248
```

### Dev keys

Keys from `dev.keys` will always be loaded as dev keys.
Dev keys may also be loaded from `prod.keys`, allowing both key sets to be in the same file.
Because both key sets use the same key sources, only a small number of root keys are needed to derive each set.

Key names that have `_dev` after the main key name but before the key index will be loaded as dev keys.

e.g. (Not actual keys)
```
master_key_0a      = B6B0F17AC61696120A15FFD41A529CBE
master_key_dev_0a  = 154A07EAFC50C6328A66C4FD2CDB277A
xci_header_key_dev = 118BA87386A242FA9DCCB06853E7A9F6
```

## Key system

This is meant to be a basic overview of the concepts used by the Switch's content key system.

### Key generations
In a nutshell, the Switch's OS contains key sources or seeds.
These seeds are useless on their own, but given a "master key" they can be used to generate the actual content keys.
This master key is the root from which all content keys are derived.
Retail and development Switches have different master keys.

The Switch uses what are called "key generations" (As in the noun, not the verb).
Each generation has its own master key which results in a different set of content keys for each one.
Content files are encrypted with the keys from the most recent generation.
e.g. A game built for system version 6.2.0 will be encrypted with the keys for 6.2.0. Older system versions would be unable to decrypt the content.

### Root keys
Root keys are the keys used to derive other keys.
Erista (original Switch hardware version) and Mariko (second hardware version) have different root keys.
Both these root keys are used to derive the same master key which will then derive other keys.

The current root key for Erista is `tsec_root_key_02`, and the key for Mariko is `mariko_kek`.
The main purpose of these keys is to generate the master key, so they're not strictly necessary for decrypting content if you have the latest master key.

These root keys, with proper security, are supposed to be hardware secrets unable to be accessed by software.

Package1 is the only content that is not encrypted with these root keys or their derivatives.
Each Erista package1 is encrypted with its own unique key, and every Mariko package1 is encrypted with `mariko_bek`.

## Key file templates

This template contains the keys needed to derive all the keys used by hactoolnet, although not all of them are needed for every task.
In fact, more than 99% of all content can be decrypted by providing only the most recent master key.

LibHac contains the key sources that keys are derived from. Only a small number of root keys need to be provided, although any keys will be loaded from the key file if present.

Providing the following keys will enable decryption of all retail content.
Every one of these keys also has a dev version. Providing them will enable decryption of all dev content.

```
# Only the latest master key is needed to decrypt the vast majority of Switch content.
master_key_0a   = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX

# Package1 keys are used to decrypt package1, the first part of the OS loaded during boot.
package1_key_00 = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
package1_key_01 = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
package1_key_02 = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
package1_key_03 = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
package1_key_04 = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
package1_key_05 = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX

# The XCI header key will decrypt the gamecard info in an XCI. Not usually needed.
xci_header_key  = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX

# Methods of obtaining the keys below are not publicly available as of Oct. 2020,
# but they're included anyway for completion's sake

# Keys for Erista package1 since firmware 6.2.0.
package1_key_06 = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
package1_key_07 = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
package1_key_08 = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
package1_key_09 = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
package1_key_0a = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX

# The Mariko boot encryption key (BEK) is used to decrypt Mariko package1.
# The Mariko key encryption key (KEK) is used to derive master keys on Mariko Switches.
# All content keys are the same on both Switch versions except for package1 keys.
# Together the Mariko BEK and KEK are enough to derive all current content keys and all
# content keys in the forseeable future except for Erista package1.
mariko_bek      = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
mariko_kek      = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
```

### Console-unique keys

This template is for console-unique keys.

```
tsec_key         = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
secure_boot_key  = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
sd_seed          = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX

# These keys can be derived from tsec_key and secure_boot_key
device_key       = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
bis_key_00       = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
bis_key_01       = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
bis_key_02       = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
bis_key_03       = XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
```

#### XTS-AES keys note

The Switch uses 128-bit XTS-AES for decrypting the built-in storage (BIS), NCA header and the SD card contents.
This encryption method uses 2 128-bit keys: a "data" or "cipher" key, and a "tweak" key.

In the key file these are stored as one 256-bit key with the data key first, followed by the tweak key.

## Complete key list
Below is a complete list of keys that are currently recognized.  
\## represents a hexadecimal number between 00 and 1F  

### Common keys

```
tsec_root_kek
package1_mac_kek
package1_kek
tsec_auth_signature_##
tsec_root_key_##

keyblob_mac_key_source
keyblob_key_source_##
keyblob_##

mariko_bek
mariko_kek
mariko_aes_class_key_##
mariko_master_kek_source_##

master_kek_source_##
master_kek_##
master_key_source
master_key_##

package1_key_##
package1_mac_key_##
package2_key_source
package2_key_##

bis_kek_source
bis_key_source_00
bis_key_source_01
bis_key_source_02
bis_key_source_03

per_console_key_source
retail_specific_aes_key_source
aes_kek_generation_source
aes_key_generation_source
titlekek_source
titlekek_##

header_kek_source
header_key_source
header_key

key_area_key_application_source
key_area_key_ocean_source
key_area_key_system_source
key_area_key_application_##
key_area_key_ocean_##
key_area_key_system_##

save_mac_kek_source
save_mac_key_source_00
save_mac_key_source_01
save_mac_sd_card_kek_source
save_mac_sd_card_key_source

sd_card_kek_source
sd_card_save_key_source
sd_card_nca_key_source
sd_card_custom_storage_key_source

xci_header_key
eticket_rsa_kek
ssl_rsa_kek
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
save_mac_key_00
save_mac_key_01

keyblob_key_##
keyblob_mac_key_##
encrypted_keyblob_##

sd_seed
save_mac_sd_card_key
```
