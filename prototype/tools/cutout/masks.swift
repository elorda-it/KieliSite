// Dump Vision masks at full image resolution as 8-bit grayscale PNGs.
// usage: masks <in> <outdir>   -> outdir/mask_inst.png, outdir/mask_person.png
import Foundation
import Vision
import CoreImage
import ImageIO
import UniformTypeIdentifiers

let a = CommandLine.arguments
let inURL = URL(fileURLWithPath: a[1]), outDir = URL(fileURLWithPath: a[2])
guard let src = CGImageSourceCreateWithURL(inURL as CFURL, nil), let cg = CGImageSourceCreateImageAtIndex(src, 0, nil) else { print("cannot read"); exit(1) }
let W = cg.width, H = cg.height

func toFloats(_ pb: CVPixelBuffer) -> (w: Int, h: Int, v: [Float]) {
  CVPixelBufferLockBaseAddress(pb, .readOnly); defer { CVPixelBufferUnlockBaseAddress(pb, .readOnly) }
  let w = CVPixelBufferGetWidth(pb), h = CVPixelBufferGetHeight(pb), bpr = CVPixelBufferGetBytesPerRow(pb)
  let base = CVPixelBufferGetBaseAddress(pb)!, fmt = CVPixelBufferGetPixelFormatType(pb)
  var v = [Float](repeating: 0, count: w * h)
  for y in 0..<h {
    let row = base.advanced(by: y * bpr)
    for x in 0..<w {
      switch fmt {
      case kCVPixelFormatType_OneComponent32Float: v[y * w + x] = row.assumingMemoryBound(to: Float.self)[x]
      case kCVPixelFormatType_OneComponent16Half: v[y * w + x] = Float(row.assumingMemoryBound(to: Float16.self)[x])
      default: v[y * w + x] = Float(row.assumingMemoryBound(to: UInt8.self)[x]) / 255
      }
    }
  }
  print("  buffer", w, "x", h, "format", fmt)
  return (w, h, v)
}
func resample(_ m: (w: Int, h: Int, v: [Float])) -> [Float] {   // bilinear to W x H
  if m.w == W && m.h == H { return m.v }
  var out = [Float](repeating: 0, count: W * H)
  for y in 0..<H { for x in 0..<W {
    let fx = (Float(x) + 0.5) * Float(m.w) / Float(W) - 0.5, fy = (Float(y) + 0.5) * Float(m.h) / Float(H) - 0.5
    let x0 = max(0, min(m.w - 1, Int(floor(fx)))), y0 = max(0, min(m.h - 1, Int(floor(fy))))
    let x1 = min(m.w - 1, x0 + 1), y1 = min(m.h - 1, y0 + 1), tx = max(0, min(1, fx - Float(x0))), ty = max(0, min(1, fy - Float(y0)))
    let a = m.v[y0 * m.w + x0] * (1 - tx) + m.v[y0 * m.w + x1] * tx, b = m.v[y1 * m.w + x0] * (1 - tx) + m.v[y1 * m.w + x1] * tx
    out[y * W + x] = a * (1 - ty) + b * ty
  } }
  return out
}
func save(_ v: [Float], _ name: String) {
  var px = v.map { UInt8(max(0, min(255, ($0 * 255).rounded()))) }
  let ctx = CGContext(data: &px, width: W, height: H, bitsPerComponent: 8, bytesPerRow: W, space: CGColorSpaceCreateDeviceGray(), bitmapInfo: CGImageAlphaInfo.none.rawValue)!
  let dst = CGImageDestinationCreateWithURL(outDir.appendingPathComponent(name) as CFURL, UTType.png.identifier as CFString, 1, nil)!
  CGImageDestinationAddImage(dst, ctx.makeImage()!, nil); CGImageDestinationFinalize(dst)
}
let handler = VNImageRequestHandler(cgImage: cg, options: [:])
let inst = VNGenerateForegroundInstanceMaskRequest()
let person = VNGeneratePersonSegmentationRequest(); person.qualityLevel = .accurate; person.outputPixelFormat = kCVPixelFormatType_OneComponent8
do {
  try handler.perform([inst, person])
  if let r = inst.results?.first {
    print("instance mask:"); save(resample(toFloats(try r.generateScaledMaskForImage(forInstances: r.allInstances, from: handler))), "mask_inst.png")
  }
  if let r = person.results?.first {
    print("person mask:"); save(resample(toFloats(r.pixelBuffer)), "mask_person.png")
  }
} catch { print("error:", error); exit(1) }
print("ok", W, "x", H)
