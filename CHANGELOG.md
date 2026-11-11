# Changelog

What each release changed for you, newest first. Each line is a commit's summary, linked to its full description and diff. Releases before 3.0.4 are described by their release commits.

## 3.0.28 - 2026-11-11

### Fixes

- Require VPNDetection 5.13.2: Unlimited, the free plan, never the Unlimited tier ([`0a7b8b4`](https://github.com/vpndetection-io/sdk-dotnet-aspnetcore/commit/0a7b8b40f853a9e5bcc8bfa81c9878e23857408c))

## 3.0.27 - 2026-11-11

### Fixes

- Require VPNDetection 5.13.1: the free plan's daily trial, never a trial key ([`c530363`](https://github.com/vpndetection-io/sdk-dotnet-aspnetcore/commit/c530363834f8068afb089768aa0bed199d06f6c1))

## 3.0.26 - 2026-11-11

### Features

- Require VPNDetection 5.13.0: trial_expired on a trial key's answers ([`7b75f89`](https://github.com/vpndetection-io/sdk-dotnet-aspnetcore/commit/7b75f893a15c5e996415c314c472dd1a42f268e3))

## 3.0.25 - 2026-11-11

### Features

- Require VPNDetection 5.12.2: the Unlimited tier and its terms ([`1d296b4`](https://github.com/vpndetection-io/sdk-dotnet-aspnetcore/commit/1d296b486d067115208501f527deb0edb5c7ced4))

## 3.0.24 - 2026-11-11

### Fixes

- Require VPNDetection 5.11.1: Retry-After read as sent, no null in an OAuth list ([`62b637f`](https://github.com/vpndetection-io/sdk-dotnet-aspnetcore/commit/62b637fab591dce05753c1a4e250b9300d396e8c))

## 3.0.23 - 2026-11-07

### Features

- Require VPNDetection 5.11.0: downloads take the build a webhook names ([`cdc50e1`](https://github.com/vpndetection-io/sdk-dotnet-aspnetcore/commit/cdc50e1632ef62e5ed05a956a9e201d595164117))

## 3.0.22 - 2026-11-06

### Fixes

- Require VPNDetection 5.10.3: the device poll asks nothing at its deadline ([`9032dfd`](https://github.com/vpndetection-io/sdk-dotnet-aspnetcore/commit/9032dfd0aa747a1419264096e3858f2c8fc8e89e))

## 3.0.21 - 2026-10-29

### Fixes

- Require VPNDetection 5.10.2: enums read by name and dates in ASCII digits alone ([`20c0d9a`](https://github.com/vpndetection-io/sdk-dotnet-aspnetcore/commit/20c0d9a937fcd102131595b25092aee1d589c40b))

## 3.0.20 - 2026-10-26

### Fixes

- Require VPNDetection 5.10.1: re-pinned to the 2026.10.25 spec ([`774fb7e`](https://github.com/vpndetection-io/sdk-dotnet-aspnetcore/commit/774fb7ef4920b39b3d2a3632458e44c7cbaf25ec))

## 3.0.19 - 2026-10-25

### Fixes

- Require VPNDetection 5.10.0: block conditions match on Location and Asn ([`ef5e699`](https://github.com/vpndetection-io/sdk-dotnet-aspnetcore/commit/ef5e699cee3e2e1e587b0fb6f27f31aefed4eb38))

## 3.0.18 - 2026-10-20

### Features

- Require VPNDetection 5.9.0: lookup results carry Location and Asn ([`ae9572d`](https://github.com/vpndetection-io/sdk-dotnet-aspnetcore/commit/ae9572d44d6f2e5f84e423b6e615fad885ac9a64))

## 3.0.17 - 2026-10-20

### Fixes

- Require VPNDetection 5.8.0: client_id_issued_at is 64-bit ([`b1bc07a`](https://github.com/vpndetection-io/sdk-dotnet-aspnetcore/commit/b1bc07a60f481a475a8ec7f14a8702d9ff1c8db0))

## 3.0.16 - 2026-10-19

### Fixes

- Require VPNDetection 5.7.0: num_ports and num_proxies in the pubproxy detail ([`f594183`](https://github.com/vpndetection-io/sdk-dotnet-aspnetcore/commit/f594183743b99d381356665117df8e4f882d2774))

## 3.0.15 - 2026-10-18

### Fixes

- Require VPNDetection 5.6.0: IsPubproxy and the pubproxy detail ([`c7b60cc`](https://github.com/vpndetection-io/sdk-dotnet-aspnetcore/commit/c7b60cc66cd35f6fcdcae99c2bebaa7ebb8adc88))

## 3.0.14 - 2026-10-17

### Features

- Require VPNDetection 5.5.0: OauthMetadata carries registration_endpoint ([`38dd148`](https://github.com/vpndetection-io/sdk-dotnet-aspnetcore/commit/38dd1487c61cd7925e34f771249619b70b9392ec))

## 3.0.13 - 2026-10-16

### Fixes

- Require VPNDetection 5.4.4: a key outside ASCII is refused where it is set ([`3210756`](https://github.com/vpndetection-io/sdk-dotnet-aspnetcore/commit/3210756ad36d28a492d6b72571118a2d1a769595))

## 3.0.12 - 2026-10-13

### Fixes

- Require VPNDetection 5.4.3: OAuth answers it cannot read are retried ([`65cad8e`](https://github.com/vpndetection-io/sdk-dotnet-aspnetcore/commit/65cad8e9c829f6b903cdc6b2e7cbbb4bdf9928aa))

## 3.0.11 - 2026-10-13

### Fixes

- Require VPNDetection 5.4.2: unreadable answers, Retry-After and keys ([`b97d0f0`](https://github.com/vpndetection-io/sdk-dotnet-aspnetcore/commit/b97d0f011a13c578a1fbdddec3476206bff31805))

## 3.0.10 - 2026-10-10

### Fixes

- Require VPNDetection 5.4.1: the spec re-pinned to 2026.10.09 ([`da36643`](https://github.com/vpndetection-io/sdk-dotnet-aspnetcore/commit/da366434c7c454b19183f6163c8ed29a4df6d27e))

## 3.0.9 - 2026-10-05

### Features

- Require VPNDetection 5.4.0: the authorization code sign-in ([`04b3f3b`](https://github.com/vpndetection-io/sdk-dotnet-aspnetcore/commit/04b3f3bf6d183368c6b4bc8e5b913c96d54e938b))

## 3.0.8 - 2026-10-04

### Fixes

- Require VPNDetection 5.3.3, re-pinned to spec 2026.10.03 ([`42e2e2c`](https://github.com/vpndetection-io/sdk-dotnet-aspnetcore/commit/42e2e2c589a7835c1b3aaa4c34ae18268b418eb5))

## 3.0.7 - 2026-09-30

### Fixes

- Require VPNDetection 5.3.2: concurrent requests from one visitor share one lookup ([`5cd7dca`](https://github.com/vpndetection-io/sdk-dotnet-aspnetcore/commit/5cd7dcaf95b5f53e92dfd5e137cb9fdd8d8e08fa))

## 3.0.6 - 2026-09-29

### Fixes

- Require VPNDetection 5.3.1: IPv4-mapped visitors are looked up, not waved through ([`ee52be4`](https://github.com/vpndetection-io/sdk-dotnet-aspnetcore/commit/ee52be43ae0197b85195c543d47f5de86ee1173b))

## 3.0.5 - 2026-09-27

### Features

- Require VPNDetection 5.3.0: OauthMetadata carries ClientIdMetadataDocumentSupported ([`7b97220`](https://github.com/vpndetection-io/sdk-dotnet-aspnetcore/commit/7b9722064319b51316356df3f2a3f43837e6fca7))

## 3.0.4 - 2026-09-23

### Fixes

- Raise the VPNDetection floor to 5.2.4 ([`3754e1f`](https://github.com/vpndetection-io/sdk-dotnet-aspnetcore/commit/3754e1fb42249fd080b631839f49785a7a3d9b91))
