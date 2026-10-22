"""Offline packaging checks; run with python3 -m unittest discover -s tests -p test_bake_animations.py."""
import unittest
from PIL import Image
from tools.bake_animations import pack


class BakeChecks(unittest.TestCase):
    def fixture(self):
        image = Image.new("RGBA", (20, 20))
        # A body, a disconnected tool head, and an antialiased particle.
        image.paste((30, 80, 150, 255), (6, 2, 12, 19))
        image.putpixel((18, 5), (190, 195, 200, 255))
        image.putpixel((1, 7), (250, 180, 10, 64))
        spec = {"body_height": 150, "frames": [{"rect": [0, 0, 20, 20], "foot": [9, 19]} for _ in range(32)]}
        return image, spec

    def test_disconnected_parts_and_partial_alpha_are_preserved(self):
        image, spec = self.fixture()
        atlas, frames = pack(image, spec)
        x, y, w, h = frames[0]["region"]
        self.assertEqual(atlas.getpixel((x + 18, y + 5)), image.getpixel((18, 5)))
        self.assertEqual(atlas.getpixel((x + 1, y + 7)), image.getpixel((1, 7)))
        self.assertEqual(atlas.crop((x, y, x + w, y + h)).tobytes(), image.tobytes())

    def test_boot_color_never_changes_explicit_pivot(self):
        image, spec = self.fixture()
        first = pack(image, spec)[1]
        image.paste((5, 5, 5, 255), (6, 16, 12, 19))
        second = pack(image, spec)[1]
        self.assertEqual(first, second)
        self.assertEqual(second[0]["foot"], [9, 19])

    def test_invalid_layout_fails_instead_of_guessing(self):
        image, spec = self.fixture()
        spec["frames"][0] = {"rect": [15, 15, 20, 20], "foot": [9, 19]}
        with self.assertRaises(ValueError):
            pack(image, spec)


if __name__ == "__main__":
    unittest.main()
