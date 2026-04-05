// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Khepri.Domain.Timelapse;

/// <summary>
/// Identifies the algorithm used to align frames of a clone project.
/// </summary>
public enum AlignmentAlgorithm
{
    FacialLandmarkWarp = 0,
    PhaseCorrelation   = 1,
    OrbFeatureMatching = 2,
    LaplacianCrop      = 3,
}
