// Refine a person cutout shot on a pure-black backdrop ("known background" matting).
// A Vision mask only says roughly where the edge is; inside a narrow band around it the
// true coverage is measured from the photo:  I = a*F + (1-a)*B,  B = black  ->  a = I/F,
// with F (the subject colour) propagated outwards from pixels just inside the edge.
// usage: matte <src image> <coarse mask png> <out.rgba> [debug dir]
import Foundation
import CoreGraphics
import ImageIO
import UniformTypeIdentifiers

let A = CommandLine.arguments
func load(_ path: String, gray: Bool) -> (Int, Int, [Float]) {
  let src = CGImageSourceCreateWithURL(URL(fileURLWithPath: path) as CFURL, nil)!, cg = CGImageSourceCreateImageAtIndex(src, 0, nil)!
  let w = cg.width, h = cg.height, ch = gray ? 1 : 4
  var px = [UInt8](repeating: 0, count: w * h * ch)
  let ctx = CGContext(data: &px, width: w, height: h, bitsPerComponent: 8, bytesPerRow: w * ch,
                      space: gray ? CGColorSpaceCreateDeviceGray() : CGColorSpace(name: CGColorSpace.sRGB)!,
                      bitmapInfo: gray ? CGImageAlphaInfo.none.rawValue : CGImageAlphaInfo.noneSkipLast.rawValue)!
  ctx.draw(cg, in: CGRect(x: 0, y: 0, width: w, height: h))
  return (w, h, px.map { Float($0) })
}
let (W, H, rgbx) = load(A[1], gray: false)
let (_, _, coarse) = load(A[2], gray: true)
let N = W * H
var R = [Float](repeating: 0, count: N), G = R, Bl = R
for i in 0..<N { R[i] = rgbx[i * 4]; G[i] = rgbx[i * 4 + 1]; Bl[i] = rgbx[i * 4 + 2] }

// signed distance to the coarse edge (chamfer 1 / 1.414): > 0 inside the person
func distTo(_ target: [Bool]) -> [Float] {
  var d = [Float](repeating: 1e9, count: N)
  for i in 0..<N where target[i] { d[i] = 0 }
  let s2: Float = 1.41421
  for y in 0..<H { for x in 0..<W { let i = y * W + x; var v = d[i]
    if x > 0 { v = min(v, d[i - 1] + 1) }
    if y > 0 { v = min(v, d[i - W] + 1); if x > 0 { v = min(v, d[i - W - 1] + s2) }; if x < W - 1 { v = min(v, d[i - W + 1] + s2) } }
    d[i] = v } }
  for y in stride(from: H - 1, through: 0, by: -1) { for x in stride(from: W - 1, through: 0, by: -1) { let i = y * W + x; var v = d[i]
    if x < W - 1 { v = min(v, d[i + 1] + 1) }
    if y < H - 1 { v = min(v, d[i + W] + 1); if x < W - 1 { v = min(v, d[i + W + 1] + s2) }; if x > 0 { v = min(v, d[i + W - 1] + s2) } }
    d[i] = v } }
  return d
}
let inside = coarse.map { $0 > 127 }
let toBg = distTo(inside.map { !$0 }), toFg = distTo(inside)
var sd = [Float](repeating: 0, count: N)
for i in 0..<N { sd[i] = inside[i] ? toBg[i] : -toFg[i] }

// band widths: wider around the hair, where the edge is soft and textured
// hair zone: the top 20% of the image height below the head's crown, as wide as the head there (+12 px)
var crown = H, hx0 = W, hx1 = 0
for y in 0..<H { for x in 0..<W where inside[y * W + x] { crown = y; break }; if crown < H { break } }
let hairBottom = min(H, crown + Int(Double(H) * 0.2))
for y in crown..<hairBottom { for x in 0..<W where inside[y * W + x] { hx0 = min(hx0, x); hx1 = max(hx1, x) } }
hx0 = max(0, hx0 - 12); hx1 = min(W - 1, hx1 + 12)
print("hair zone: rows", crown, "-", hairBottom, " cols", hx0, "-", hx1)
func hairZone(_ x: Int, _ y: Int) -> Bool { return y >= crown && y < hairBottom && x >= hx0 && x <= hx1 }
var rIn = [Float](repeating: 3, count: N), rOut = [Float](repeating: 5, count: N)
let unsure = coarse.map { $0 > 38 && $0 < 217 }          // Vision itself was not sure here
let nearUnsure = distTo(unsure).map { $0 <= 3 }           // ...and the walls around such places (e.g. the arm gap)
// a gap has body on both sides within a few px; the outer silhouette has open backdrop on one side
var enclosed = [Bool](repeating: false, count: N)
for y in 0..<H { for x in 0..<W where nearUnsure[y * W + x] {
  for (dx, dy) in [(1, 0), (0, 1), (1, 1), (1, -1)] {
    var fwd = false, back = false
    for k in 1...8 {
      let xf = x + dx * k, yf = y + dy * k, xb = x - dx * k, yb = y - dy * k
      if !fwd && xf >= 0 && xf < W && yf >= 0 && yf < H && coarse[yf * W + xf] > 217 { fwd = true }
      if !back && xb >= 0 && xb < W && yb >= 0 && yb < H && coarse[yb * W + xb] > 217 { back = true }
    }
    if fwd && back { enclosed[y * W + x] = true; break }
  }
} }
for y in 0..<H { for x in 0..<W where hairZone(x, y) { rIn[y * W + x] = 5; rOut[y * W + x] = 10 } }

// background noise level next to the person (webp ringing, grain)
var near: [Float] = []
for i in 0..<N where sd[i] < -rOut[i] - 1 && sd[i] > -rOut[i] - 25 { near.append(max(R[i], G[i], Bl[i])) }
near.sort()
let n0 = near.isEmpty ? 3 : near[Int(Double(near.count - 1) * 0.995)]
print("backdrop noise (99.5%):", n0, "from", near.count, "px")

// subject colour F: Gaussian-weighted mean of sure-foreground pixels close to the edge,
// falling back to wider kernels where the band is far from any sample
func blur(_ v: [Float], _ sigma: Float) -> [Float] {
  let r = Int(ceil(sigma * 3)); var k = [Float](repeating: 0, count: 2 * r + 1)
  for j in -r...r { k[j + r] = exp(-Float(j * j) / (2 * sigma * sigma)) }
  var t = [Float](repeating: 0, count: N), o = t
  for y in 0..<H { for x in 0..<W { var s: Float = 0
    for j in -r...r { let xx = x + j; if xx >= 0 && xx < W { s += v[y * W + xx] * k[j + r] } }
    t[y * W + x] = s } }
  for y in 0..<H { for x in 0..<W { var s: Float = 0
    for j in -r...r { let yy = y + j; if yy >= 0 && yy < H { s += t[yy * W + x] * k[j + r] } }
    o[y * W + x] = s } }
  return o
}
var sample = [Float](repeating: 0, count: N)
for i in 0..<N where sd[i] > rIn[i] && sd[i] < rIn[i] + 7 && !unsure[i] && max(R[i], G[i], Bl[i]) > 12 { sample[i] = 1 }
var FR = [Float](repeating: -1, count: N), FG = FR, FB = FR
for sigma in [Float(3), 7, 16] {
  let w = blur(sample, sigma)
  let r = blur(zip(sample, R).map { $0 * $1 }, sigma), g = blur(zip(sample, G).map { $0 * $1 }, sigma), b = blur(zip(sample, Bl).map { $0 * $1 }, sigma)
  for i in 0..<N where FR[i] < 0 && w[i] > 0.05 { FR[i] = r[i] / w[i]; FG[i] = g[i] / w[i]; FB[i] = b[i] / w[i] }
}

// alpha
var alpha = [Float](repeating: 0, count: N)
var keyed = [Bool](repeating: false, count: N)
var band = 0
for i in 0..<N {
  let measured = nearUnsure[i] || (sd[i] >= -rOut[i] && sd[i] <= rIn[i])
  if !measured { alpha[i] = sd[i] > 0 ? 1 : 0; continue }
  band += 1
  if FR[i] < 0 { alpha[i] = sd[i] > 0 ? 1 : 0; continue }
  let fl = (FR[i] * FR[i] + FG[i] * FG[i] + FB[i] * FB[i]).squareRoot()
  if fl < n0 * 2 { alpha[i] = sd[i] > 0 ? 1 : 0; continue }
  let proj = (R[i] * FR[i] + G[i] * FG[i] + Bl[i] * FB[i]) / fl        // I projected on the subject colour
  var a = (proj - n0) / (fl - n0)
  a = max(0, min(1, a))
  // Where Vision was unsure (arm gaps, cuff buttons) a dark subject is easily mistaken for the
  // backdrop by the ratio above. The backdrop is 0–3, deep fabric shadow is >= 10: key on that.
  let x = i % W, y = i / W
  if nearUnsure[i] && enclosed[i] && !hairZone(x, y) {   // gaps only: on the outer silhouette dark pixels are real mixes
    let ak = max(0, min(1, (max(R[i], G[i], Bl[i]) - 4) / 6))
    let darkF = max(0, min(1, (150 - max(FR[i], FG[i], FB[i])) / 50))
    a = max(a, ak * darkF); keyed[i] = true
  }
  alpha[i] = a
}
// tidy: drop faint haze and specks outside, fill pinholes inside
var clean = alpha
for y in 1..<(H - 1) { for x in 1..<(W - 1) { let i = y * W + x
  if !(nearUnsure[i] || (sd[i] >= -rOut[i] && sd[i] <= rIn[i])) { continue }
  var nb: [Float] = []; for dy in -1...1 { for dx in -1...1 { nb.append(alpha[i + dy * W + dx]) } }
  nb.sort(); let med = nb[4]
  var a: Float
  if sd[i] < 0 { a = med < 0.02 ? 0 : min(alpha[i], med + 0.25) }   // outside the edge: no lone specks
  else { a = max(alpha[i], med) }                                   // inside: no single-pixel pinholes
  if a < 0.05 { a = 0 } else if a > 0.96 { a = 1 }
  clean[i] = a } }
alpha = clean
func morph(_ v: [Float], _ r: Int, _ isMax: Bool) -> [Float] {
  var t = v, o = v
  for y in 0..<H { for x in 0..<W { var m = v[y * W + x]
    for j in max(0, x - r)...min(W - 1, x + r) { m = isMax ? max(m, v[y * W + j]) : min(m, v[y * W + j]) }
    t[y * W + x] = m } }
  for y in 0..<H { for x in 0..<W { var m = t[y * W + x]
    for j in max(0, y - r)...min(H - 1, y + r) { m = isMax ? max(m, t[j * W + x]) : min(m, t[j * W + x]) }
    o[y * W + x] = m } }
  return o
}
let closed = morph(morph(alpha, 2, true), 2, false)
for y in 0..<H { for x in 0..<W where hairZone(x, y) { let i = y * W + x; if sd[i] > -1 { alpha[i] = max(alpha[i], closed[i]) } } }
// the backdrop key gives hard 1-px steps inside gaps: soften them into anti-aliased edges
var soft = alpha
for y in 1..<(H - 1) { for x in 1..<(W - 1) where keyed[y * W + x] {
  var t: Float = 0; for dy in -1...1 { for dx in -1...1 { t += alpha[(y + dy) * W + x + dx] } }
  soft[y * W + x] = t / 9 } }
alpha = soft
// shape clean-up: fill tiny holes enclosed by the body, drop tiny islands detached from it
func components(_ member: (Int) -> Bool, _ eight: Bool) -> [[Int]] {
  var seen = [Bool](repeating: false, count: N), out: [[Int]] = []
  for s0 in 0..<N where member(s0) && !seen[s0] {
    var comp: [Int] = [], stack = [s0]; seen[s0] = true
    while let j = stack.popLast() {
      comp.append(j); let x = j % W, y = j / W
      for dy in -1...1 { for dx in -1...1 where (dx != 0 || dy != 0) && (eight || dx == 0 || dy == 0) {
        let xx = x + dx, yy = y + dy
        if xx < 0 || yy < 0 || xx >= W || yy >= H { continue }
        let k = yy * W + xx; if !seen[k] && member(k) { seen[k] = true; stack.append(k) } } }
    }
    out.append(comp)
  }
  return out
}
var filled = 0, dropped = 0
for level in [Float(0.5), 0.9] {   // clear holes first, then enclosed semi-transparent pockets
  for comp in components({ alpha[$0] < level }, false) {
    let touchesBorder = comp.contains { let x = $0 % W, y = $0 / W; return x == 0 || y == 0 || x == W - 1 || y == H - 1 }
    if !touchesBorder && comp.count < 40 { for j in comp { alpha[j] = 1 }; filled += comp.count }
  }
}
for comp in components({ alpha[$0] >= 0.1 }, true) where comp.count < 30 { for j in comp { alpha[j] = 0 }; dropped += comp.count }
print("holes filled:", filled, "px, islands dropped:", dropped, "px")
print("band px:", band)

// colours: un-mix the black backdrop (C = I / a), blend towards F where a is tiny
var out = [UInt8](repeating: 0, count: N * 4)
for i in 0..<N {
  let a = alpha[i]
  var r = R[i], g = G[i], b = Bl[i]
  if a > 0 && a < 1 {
    let ur = min(255, R[i] / a), ug = min(255, G[i] / a), ub = min(255, Bl[i] / a)
    let w = max(0, min(1, (a - 0.1) / 0.4))
    let fr = FR[i] < 0 ? ur : FR[i], fg = FG[i] < 0 ? ug : FG[i], fb = FB[i] < 0 ? ub : FB[i]
    r = w * ur + (1 - w) * fr; g = w * ug + (1 - w) * fg; b = w * ub + (1 - w) * fb
    // an edge pixel may not be much brighter than the subject next to it (kills white specks in dark hair)
    if FR[i] >= 0 { r = min(r, max(R[i], FR[i] * 1.5 + 24)); g = min(g, max(G[i], FG[i] * 1.5 + 24)); b = min(b, max(Bl[i], FB[i] * 1.5 + 24)) }
  }
  out[i * 4] = UInt8(max(0, min(255, r.rounded()))); out[i * 4 + 1] = UInt8(max(0, min(255, g.rounded())))
  out[i * 4 + 2] = UInt8(max(0, min(255, b.rounded()))); out[i * 4 + 3] = UInt8(max(0, min(255, (a * 255).rounded())))
}
try! Data(out).write(to: URL(fileURLWithPath: A[3]))
if A.count > 4 {   // debug: trimap
  var tri = [UInt8](repeating: 0, count: N)
  for i in 0..<N { tri[i] = sd[i] > rIn[i] ? 255 : (sd[i] < -rOut[i] ? 0 : 128) }
  try! Data(tri).write(to: URL(fileURLWithPath: A[4] + "/trimap.gray"))
}
print("ok", W, "x", H)
