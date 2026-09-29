Set-Location (Split-Path $MyInvocation.MyCommand.Path -Parent)

git clone https://github.com/SagerNet/sing-box -b 'v1.10.7' src
if ( -Not $? ) {
    exit $lastExitCode
}
Set-Location src

$Env:CGO_ENABLED='0'
$Env:GOROOT_FINAL='/usr'

$Env:GOOS='windows'
$Env:GOARCH='amd64'
go build -v -trimpath -tags "with_quic,with_dhcp,with_wireguard,with_ech,with_utls,with_v2rayapi,with_gvisor" -asmflags '-s -w' -ldflags '-s -w -buildid=' -o '..\..\release\sing-box.exe' './cmd/sing-box'
exit $lastExitCode
