#!/usr/bin/env python3
"""
Eye camera intrinsics calibration (checkerboard).

Usage:
  1. Capture 15-25 still photos of a printed checkerboard with the Eye camera
     (vary angle/distance, fill the frame). Use the XREAL capture app or the
     RGBCamera sample's recording, then extract frames.
  2. python3 calibrate_intrinsics.py --images "shots/*.jpg" --cols 9 --rows 6 --square 0.024
  3. Copy fx/fy/cx/cy (+ distortion) into the EyeCalibration asset in Unity.

Notes:
  - --square is the printed square edge length in meters (only affects extrinsics
    scale during calibration, intrinsics are unaffected; still pass the real value).
  - Calibrate at the SAME resolution / capture mode you will use at runtime.
    If EIS cannot be disabled, calibrate with EIS on and expect some residual error.
"""
import argparse, glob, json, sys
import numpy as np
import cv2

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--images", required=True, help="glob pattern, e.g. 'shots/*.jpg'")
    ap.add_argument("--cols", type=int, default=9, help="inner corners per row")
    ap.add_argument("--rows", type=int, default=6, help="inner corners per column")
    ap.add_argument("--square", type=float, default=0.024, help="square size in meters")
    args = ap.parse_args()

    pattern = (args.cols, args.rows)
    objp = np.zeros((args.cols * args.rows, 3), np.float32)
    objp[:, :2] = np.mgrid[0:args.cols, 0:args.rows].T.reshape(-1, 2) * args.square

    objpoints, imgpoints, size = [], [], None
    files = sorted(glob.glob(args.images))
    if not files:
        sys.exit(f"no images match {args.images}")

    for f in files:
        img = cv2.imread(f)
        gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
        size = gray.shape[::-1]
        ok, corners = cv2.findChessboardCorners(gray, pattern, None)
        print(("OK  " if ok else "SKIP") + f" {f}")
        if not ok:
            continue
        corners = cv2.cornerSubPix(
            gray, corners, (11, 11), (-1, -1),
            (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 30, 0.001))
        objpoints.append(objp)
        imgpoints.append(corners)

    if len(objpoints) < 8:
        sys.exit(f"only {len(objpoints)} usable views; capture more (>=15 recommended)")

    rms, K, dist, _, _ = cv2.calibrateCamera(objpoints, imgpoints, size, None, None)

    out = {
        "rms_reprojection_error_px": round(float(rms), 4),
        "resolution": {"width": size[0], "height": size[1]},
        "fx": float(K[0, 0]), "fy": float(K[1, 1]),
        "cx": float(K[0, 2]), "cy": float(K[1, 2]),
        "distortion_k1k2p1p2k3": [float(d) for d in dist.ravel()[:5]],
        "views_used": len(objpoints),
    }
    print(json.dumps(out, indent=2))
    if rms > 1.0:
        print("WARNING: RMS > 1px. Check focus/EIS/board flatness.", file=sys.stderr)

if __name__ == "__main__":
    main()
