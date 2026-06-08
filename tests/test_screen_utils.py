import unittest

import numpy as np

from fh6_scanner.screen_utils import diff_score


class DifferenceScoreTests(unittest.TestCase):
    def test_identical_images_have_zero_score(self):
        image = np.zeros((12, 16, 3), dtype=np.uint8)
        self.assertEqual(diff_score(image, image.copy()), 0.0)

    def test_score_resizes_current_region_to_template(self):
        template = np.zeros((10, 10, 3), dtype=np.uint8)
        current = np.full((20, 20, 3), 20, dtype=np.uint8)
        self.assertAlmostEqual(diff_score(current, template), 20.0)


if __name__ == "__main__":
    unittest.main()
