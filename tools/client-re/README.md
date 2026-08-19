# client-re — our clean client-binary analyzer

Reverse-engineers Carbine's WildStar client (source #1) to pin protocol formats
**from the client itself**, never from any emulator/NF source. Built for the
No-NF discipline (`provenance/NO-NF.md`): the client is a fact; NF is not a source.

Starlight tooling — our own analysis code over MIT libraries (`capstone`,
`pefile`). Install: `pip install capstone pefile`.

## Usage

```
python sts_re.py strings   <client.dll>
python sts_re.py xrefs     <client.dll> 0xVA[,0xVA...]
python sts_re.py fieldrefs <client.dll> 0xStart 0xEnd
python sts_re.py disasm    <client.dll> 0xStart 0xEnd
```

`fieldrefs` is the workhorse: give it a function's range and it lists every
string that code references — which reveals a message handler's field set at a
glance (e.g. the STS login handler at `.text` 0x0A360 references `KeyData`,
`Request`, `ServerRand`, `ServerPublicKey`, `ServerSignature`).

## What it has pinned

The STS login message formats (`spec/protocol/sts.md`): the LoginStart reply's
`<KeyData>` base64 element, the symmetric KeyData request, the field dictionary.
Analysis of the client only — zero NF, zero corpus, zero decompiled realm.
